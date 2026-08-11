using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.RestoreVerification;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Environments = Microsoft.Extensions.Hosting.Environments;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Controller-level integration coverage for content-validation + restore-drill alias endpoints.
/// </summary>
public sealed class AdminBackupContentValidationAndDrillEndpointTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task GetContentValidation_Manager_CrossTenant_Returns404()
    {
        await using var db = CreateDb();
        var otherRunId = await SeedTenantRunAsync(db, TenantB);

        var content = new Mock<IBackupContentValidationService>(MockBehavior.Strict);
        var controller = CreateController(
            db,
            Roles.Manager,
            TenantA,
            contentValidation: content.Object);

        var result = await controller.GetContentValidation(otherRunId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        content.Verify(
            c => c.GetOrRunValidationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetContentValidation_Manager_OwnTenant_Returns200()
    {
        await using var db = CreateDb();
        var ownRunId = await SeedTenantRunAsync(db, TenantA);

        var content = new Mock<IBackupContentValidationService>();
        content
            .Setup(c => c.GetOrRunValidationAsync(ownRunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackupContentValidationDto
            {
                RunId = ownRunId,
                ValidatedAtUtc = DateTime.UtcNow,
                OverallStatus = BackupContentValidationStatuses.Passed,
                Strategy = nameof(BackupStrategyKind.Tenant),
                Summary = "ok",
            });

        var controller = CreateController(
            db,
            Roles.Manager,
            TenantA,
            contentValidation: content.Object);

        var result = await controller.GetContentValidation(ownRunId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<BackupContentValidationDto>(ok.Value);
        Assert.Equal(ownRunId, dto.RunId);
        Assert.Equal(BackupContentValidationStatuses.Passed, dto.OverallStatus);
    }

    [Fact]
    public async Task RunRestoreDrill_SettingsManage_Returns202()
    {
        await using var db = CreateDb();
        var run = new RestoreVerificationRun
        {
            Id = Guid.NewGuid(),
            Status = RestoreVerificationStatus.Queued,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            RequestedAt = DateTime.UtcNow,
        };

        var trigger = new Mock<IRestoreVerificationManualTriggerService>();
        trigger
            .Setup(t => t.EnqueueManualAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestoreVerificationManualTriggerResult
            {
                Run = run,
                OrchestrationState = RestoreVerificationTriggerOrchestrationState.NewlyQueued,
            });

        var controller = CreateController(
            db,
            Roles.SuperAdmin,
            tenantId: null,
            restoreDrill: trigger.Object,
            includeSettingsManage: true);

        var result = await controller.RunRestoreDrill(
            new RunRestoreDrillRequestDto { BackupRunId = null },
            CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result.Result);
        var dto = Assert.IsType<RestoreDrillResultDto>(accepted.Value);
        Assert.Equal(run.Id, dto.RunId);
        Assert.True(dto.Success);
        Assert.Equal(nameof(RestoreVerificationStatus.Queued), dto.Status);
    }

    [Fact]
    public async Task GetOrRunValidation_ReusesCachedVerification()
    {
        var dir = Path.Combine(Path.GetTempPath(), "regkasse-content-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var tenantId = Guid.NewGuid();
            var runId = Guid.NewGuid();
            var counts = new Dictionary<string, int>
            {
                ["products.json"] = 0,
                ["categories.json"] = 0,
                ["customers.json"] = 0,
                ["payment_details.json"] = 1,
                ["receipts.json"] = 1,
            };
            var manifestName = "manifest.json";
            await File.WriteAllTextAsync(
                Path.Combine(dir, manifestName),
                System.Text.Json.JsonSerializer.Serialize(new { tableRowCounts = counts }));

            await using var db = CreateDb();
            db.BackupRuns.Add(new BackupRun
            {
                Id = runId,
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.Tenant,
                TenantId = tenantId,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "TenantLogical",
                RequestedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Artifacts =
                {
                    new BackupArtifact
                    {
                        ArtifactType = BackupArtifactType.VerificationManifest,
                        StorageDescriptor = manifestName,
                        ContentHashSha256 = new string('c', 64),
                        CreatedAt = DateTime.UtcNow,
                    }
                }
            });
            await db.SaveChangesAsync();

            var sut = CreateContentSut(db, dir);
            var first = await sut.ValidateContentAsync(runId);
            Assert.NotNull(first.VerificationId);
            var countAfterFirst = await db.BackupVerifications.CountAsync();

            var second = await sut.GetOrRunValidationAsync(runId);
            Assert.Equal(first.VerificationId, second.VerificationId);
            Assert.Equal(countAfterFirst, await db.BackupVerifications.CountAsync());
        }
        finally
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin_backup_cv_drill_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static async Task<Guid> SeedTenantRunAsync(AppDbContext db, Guid tenantId)
    {
        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.Tenant,
            TenantId = tenantId,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "TenantLogical",
            IdempotencyKey = $"tenant:{tenantId:D}:seed",
            RequestedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow.AddMinutes(-50),
        });
        await db.SaveChangesAsync();
        return runId;
    }

    private static BackupContentValidationService CreateContentSut(AppDbContext db, string stagingRoot)
    {
        var opts = new BackupOptions { ArtifactStagingRoot = stagingRoot, EncryptionEnabled = false };
        var monitor = new Mock<IOptionsMonitor<BackupOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(opts);
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        return new BackupContentValidationService(
            db,
            monitor.Object,
            new BackupEncryptionService(monitor.Object, NullLogger<BackupEncryptionService>.Instance),
            env.Object,
            NullLogger<BackupContentValidationService>.Instance);
    }

    private static AdminBackupController CreateController(
        AppDbContext db,
        string role,
        Guid? tenantId,
        IBackupContentValidationService? contentValidation = null,
        IRestoreVerificationManualTriggerService? restoreDrill = null,
        bool includeSettingsManage = false)
    {
        var host = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development);
        var policy = BackupArtifactPipelinePolicyEvaluator.Evaluate(new BackupOptions(), host);
        var readiness = Mock.Of<IBackupOperationalReadiness>(r => r.GetArtifactPipelinePolicy() == policy);
        var options = new Mock<IOptionsMonitor<BackupOptions>>();
        options.Setup(o => o.CurrentValue).Returns(new BackupOptions());

        var controller = new AdminBackupController(
            Mock.Of<IBackupManualTriggerService>(),
            new BackupRunQueryService(db),
            new BackupRunService(
                db,
                Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>(),
                options.Object,
                host,
                NullLogger<BackupRunService>.Instance),
            Mock.Of<IBackupRecoverabilitySummaryService>(),
            Mock.Of<IRestoreOrchestrationBoundary>(),
            readiness,
            options.Object,
            Mock.Of<IBackupArtifactDownloadService>(),
            Mock.Of<IAuditLogService>(),
            NullLogger<AdminBackupController>.Instance,
            host,
            db,
            Mock.Of<IBackupSettingsAdminService>(),
            Mock.Of<IBackupDashboardStatsService>(),
            Mock.Of<IBackupComplianceStatusService>(),
            Mock.Of<IBackupStorageCostService>(),
            Mock.Of<IPitrService>(),
            Mock.Of<IBackupVerificationReportService>(),
            Mock.Of<IBackupChecksumVerificationService>(),
            contentValidation ?? Mock.Of<IBackupContentValidationService>(),
            restoreDrill ?? Mock.Of<IRestoreVerificationManualTriggerService>(),
            Mock.Of<ICurrentTenantAccessor>(a => a.TenantId == tenantId),
            new BackupRunTenantAccessService(db),
            Mock.Of<IBackupArtifactImportService>(),
            Mock.Of<IBackupTimeEstimator>(),
            Mock.Of<IDownloadSecurityService>());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "operator-1"),
            new(ClaimTypes.Role, role),
            new(PermissionCatalog.PermissionClaimType, AppPermissions.SettingsView),
            new(PermissionCatalog.PermissionClaimType, AppPermissions.BackupManage),
        };
        if (includeSettingsManage || role == Roles.SuperAdmin)
            claims.Add(new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.SettingsManage));

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
        };
        http.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "corr-cv-drill";
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }
}
