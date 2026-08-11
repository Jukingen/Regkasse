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

public sealed class RestoreDrillAliasTests
{
    [Fact]
    public async Task RunRestoreDrill_DelegatesToRestoreVerificationTrigger()
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

        var controller = CreateController(db, trigger.Object);
        var result = await controller.RunRestoreDrill(
            new RunRestoreDrillRequestDto { IdempotencyKey = "drill-1" },
            CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result.Result);
        var dto = Assert.IsType<RestoreDrillResultDto>(accepted.Value);
        Assert.Equal(run.Id, dto.RunId);
        Assert.True(dto.Success);
        Assert.Equal(nameof(RestoreVerificationStatus.Queued), dto.Status);
        Assert.True(dto.NewQueuedRunCreated);
        Assert.Empty(dto.Errors);
        Assert.Equal("/api/admin/restore-verification/trigger", dto.AliasOf);
        trigger.Verify(
            t => t.EnqueueManualAsync(
                "operator-1",
                "corr-drill",
                "drill-1",
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunRestoreDrill_PassesBackupRunIdToTrigger()
    {
        await using var db = CreateDb();
        var backupRunId = Guid.NewGuid();
        var run = new RestoreVerificationRun
        {
            Id = Guid.NewGuid(),
            Status = RestoreVerificationStatus.Queued,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            RequestedAt = DateTime.UtcNow,
            SourceBackupRunId = backupRunId,
        };

        var trigger = new Mock<IRestoreVerificationManualTriggerService>();
        trigger
            .Setup(t => t.EnqueueManualAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                backupRunId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestoreVerificationManualTriggerResult
            {
                Run = run,
                OrchestrationState = RestoreVerificationTriggerOrchestrationState.NewlyQueued,
            });

        var controller = CreateController(db, trigger.Object);
        var result = await controller.RunRestoreDrill(
            new RunRestoreDrillRequestDto { BackupRunId = backupRunId },
            CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result.Result);
        var dto = Assert.IsType<RestoreDrillResultDto>(accepted.Value);
        Assert.Equal(backupRunId, dto.SourceBackupRunId);
        Assert.True(dto.Success);
        trigger.Verify(
            t => t.EnqueueManualAsync(
                "operator-1",
                "corr-drill",
                null,
                backupRunId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunRestoreDrill_UnknownBackupRun_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var trigger = new Mock<IRestoreVerificationManualTriggerService>();
        trigger
            .Setup(t => t.EnqueueManualAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("missing"));

        var controller = CreateController(db, trigger.Object);
        var result = await controller.RunRestoreDrill(
            new RunRestoreDrillRequestDto { BackupRunId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task EnqueueManual_WithValidBackupRunId_PinsSourceBackupRunId()
    {
        await using var db = CreateDb();
        var backupRunId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = backupRunId,
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow.AddMinutes(-50),
            Artifacts =
            {
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = "dump.dump",
                    CreatedAt = DateTime.UtcNow,
                }
            }
        });
        await db.SaveChangesAsync();

        var ro = new Mock<IOptionsMonitor<RestoreVerificationOptions>>();
        ro.Setup(m => m.CurrentValue).Returns(new RestoreVerificationOptions { DumpFallbackDepth = 7 });
        var svc = new RestoreVerificationManualTriggerService(
            db,
            ro.Object,
            NullLogger<RestoreVerificationManualTriggerService>.Instance);

        var result = await svc.EnqueueManualAsync("u1", "c1", null, backupRunId);

        Assert.Equal(RestoreVerificationTriggerOrchestrationState.NewlyQueued, result.OrchestrationState);
        Assert.Equal(backupRunId, result.Run.SourceBackupRunId);
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"drill_alias_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static AdminBackupController CreateController(
        AppDbContext db,
        IRestoreVerificationManualTriggerService trigger)
    {
        var host = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development);
        var policy = BackupArtifactPipelinePolicyEvaluator.Evaluate(new BackupOptions(), host);
        var readiness = Mock.Of<IBackupOperationalReadiness>(r => r.GetArtifactPipelinePolicy() == policy);
        var options = new Mock<IOptionsMonitor<BackupOptions>>();
        options.Setup(o => o.CurrentValue).Returns(new BackupOptions());

        var c = new AdminBackupController(
            Mock.Of<IBackupManualTriggerService>(),
            Mock.Of<IBackupRunQueryService>(),
            Mock.Of<IBackupRunService>(),
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
            Mock.Of<IBackupContentValidationService>(),
            trigger,
            Mock.Of<ICurrentTenantAccessor>(),
            Mock.Of<IBackupRunTenantAccessService>(),
            Mock.Of<IBackupArtifactImportService>(),
            Mock.Of<IBackupTimeEstimator>(),
            Mock.Of<IDownloadSecurityService>());

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "operator-1"),
                    new Claim(ClaimTypes.Role, "SuperAdmin"),
                    new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.SettingsManage),
                },
                "Test")),
        };
        http.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "corr-drill";
        c.ControllerContext = new ControllerContext { HttpContext = http };
        return c;
    }
}
