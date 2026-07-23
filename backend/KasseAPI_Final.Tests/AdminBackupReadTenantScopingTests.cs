using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
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

public sealed class AdminBackupReadTenantScopingTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin_backup_read_scope_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static IOptionsMonitor<BackupOptions> OptionsMonitor(BackupOptions value)
    {
        var mock = new Mock<IOptionsMonitor<BackupOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    private static AdminBackupController CreateController(
        AppDbContext db,
        string role,
        Guid? tenantId,
        IBackupRunTenantAccessService tenantAccess,
        IBackupRunQueryService? query = null)
    {
        var host = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development);
        var policy = BackupArtifactPipelinePolicyEvaluator.Evaluate(new BackupOptions(), host);
        var readiness = Mock.Of<IBackupOperationalReadiness>(r => r.GetArtifactPipelinePolicy() == policy);

        var controller = new AdminBackupController(
            Mock.Of<IBackupManualTriggerService>(),
            query ?? new BackupRunQueryService(db),
            new BackupRunService(
                db,
                Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>(),
                OptionsMonitor(new BackupOptions()),
                host,
                NullLogger<BackupRunService>.Instance),
            Mock.Of<IBackupRecoverabilitySummaryService>(),
            Mock.Of<IRestoreOrchestrationBoundary>(),
            readiness,
            OptionsMonitor(new BackupOptions()),
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
            Mock.Of<ICurrentTenantAccessor>(a => a.TenantId == tenantId),
            tenantAccess,
            Mock.Of<IBackupArtifactImportService>(),
            Mock.Of<IBackupTimeEstimator>(),
            Mock.Of<IDownloadSecurityService>());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "manager-1"),
            new(ClaimTypes.Role, role),
            new(PermissionCatalog.PermissionClaimType, AppPermissions.SettingsView),
            new(PermissionCatalog.PermissionClaimType, AppPermissions.BackupManage),
        };
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
        };
        http.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "corr-read";
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static async Task SeedRunsAsync(AppDbContext db)
    {
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.Tenant,
                TenantId = TenantA,
                IdempotencyKey = $"manual-tenant-{TenantA:D}-1",
                RequestedAt = DateTime.UtcNow.AddHours(-1),
            },
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.Tenant,
                TenantId = TenantB,
                IdempotencyKey = $"manual-tenant-{TenantB:D}-2",
                RequestedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetHistory_Manager_WithoutTenantContext_Returns400()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, Roles.Manager, tenantId: null, new BackupRunTenantAccessService(db));

        var result = await controller.GetHistory(1, 20, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetHistory_Manager_SeesOnlyOwnTenantRuns()
    {
        await using var db = CreateDb();
        await SeedRunsAsync(db);
        var controller = CreateController(db, Roles.Manager, TenantA, new BackupRunTenantAccessService(db));

        var result = await controller.GetHistory(1, 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<BackupHistoryResponseDto>(ok.Value);
        Assert.Single(body.Items);
        Assert.Contains(TenantA.ToString("D"), body.Items[0].IdempotencyKey, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRunById_Manager_CrossTenant_Returns404()
    {
        await using var db = CreateDb();
        await SeedRunsAsync(db);
        var otherRunId = await db.BackupRuns
            .Where(r => r.IdempotencyKey!.Contains(TenantB.ToString("D")))
            .Select(r => r.Id)
            .FirstAsync();
        var controller = CreateController(db, Roles.Manager, TenantA, new BackupRunTenantAccessService(db));

        var result = await controller.GetRunById(otherRunId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetRunById_Manager_OwnTenant_Returns200()
    {
        await using var db = CreateDb();
        await SeedRunsAsync(db);
        var ownRunId = await db.BackupRuns
            .Where(r => r.IdempotencyKey!.Contains(TenantA.ToString("D")))
            .Select(r => r.Id)
            .FirstAsync();
        var controller = CreateController(db, Roles.Manager, TenantA, new BackupRunTenantAccessService(db));

        var result = await controller.GetRunById(ownRunId, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetRunById_Manager_CannotReadScheduledSystemRun()
    {
        await using var db = CreateDb();
        var scheduledId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = scheduledId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "Fake",
            Strategy = BackupStrategyKind.System,
            TenantId = null,
            RequestedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, Roles.Manager, TenantA, new BackupRunTenantAccessService(db));
        var result = await controller.GetRunById(scheduledId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetLatestVerification_Manager_WithoutTenantContext_Returns400()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, Roles.Manager, tenantId: null, new BackupRunTenantAccessService(db));

        var result = await controller.GetLatestVerification(CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetLatestVerification_Manager_SeesAccessibleRunOnly()
    {
        await using var db = CreateDb();
        var runA = Guid.NewGuid();
        var runB = Guid.NewGuid();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = runA,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.Tenant,
                TenantId = TenantA,
                RequestedAt = DateTime.UtcNow.AddHours(-2),
            },
            new BackupRun
            {
                Id = runB,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.Tenant,
                TenantId = TenantB,
                RequestedAt = DateTime.UtcNow,
            });
        db.BackupVerifications.AddRange(
            new BackupVerification
            {
                Id = Guid.NewGuid(),
                BackupRunId = runA,
                Status = BackupVerificationStatus.Passed,
                StartedAt = DateTime.UtcNow.AddHours(-1),
                VerifierSource = "test",
            },
            new BackupVerification
            {
                Id = Guid.NewGuid(),
                BackupRunId = runB,
                Status = BackupVerificationStatus.Passed,
                StartedAt = DateTime.UtcNow,
                VerifierSource = "test",
            });
        await db.SaveChangesAsync();

        var controller = CreateController(db, Roles.Manager, TenantA, new BackupRunTenantAccessService(db));
        var result = await controller.GetLatestVerification(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<BackupVerificationResponseDto>(ok.Value);
        Assert.Equal(runA, dto.BackupRunId);
    }

    [Fact]
    public async Task GetDashboardStats_Manager_WithoutTenantContext_Returns400()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, Roles.Manager, tenantId: null, new BackupRunTenantAccessService(db));

        var result = await controller.GetDashboardStats(CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetRecoverabilitySummary_Manager_WithoutTenantContext_Returns400()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, Roles.Manager, tenantId: null, new BackupRunTenantAccessService(db));

        var result = await controller.GetRecoverabilitySummary(CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
