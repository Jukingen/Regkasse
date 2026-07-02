using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
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

/// <summary>
/// POST /api/admin/backup/trigger tenant-scoping guard: a tenant-scoped role (Manager with backup.manage)
/// may only enqueue while bound to a resolved tenant context; SuperAdmin operates deployment-wide.
/// The endpoint never accepts a client-supplied tenantId, so cross-tenant triggering is impossible by construction.
/// </summary>
public sealed class AdminBackupTriggerTenantScopingTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin_backup_trigger_scope_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static IOptionsMonitor<BackupOptions> OptionsMonitor(BackupOptions value)
    {
        var mock = new Mock<IOptionsMonitor<BackupOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    private static BackupManualTriggerOutcome NewRunQueuedOutcome() => new()
    {
        Run = new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Queued,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = BackupExecutionAdapterKind.Fake.ToString(),
            RequestedAt = DateTime.UtcNow,
            QueuedAt = DateTime.UtcNow,
        },
        Kind = BackupManualTriggerResultKind.NewRunQueued,
    };

    private static (AdminBackupController controller, Mock<IBackupManualTriggerService> trigger) CreateController(
        AppDbContext db,
        string role,
        Guid? tenantId)
    {
        var trigger = new Mock<IBackupManualTriggerService>();
        trigger
            .Setup(t => t.RequestManualBackupAsync(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewRunQueuedOutcome());

        var host = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development);
        var policy = BackupArtifactPipelinePolicyEvaluator.Evaluate(new BackupOptions(), host);
        var readiness = Mock.Of<IBackupOperationalReadiness>(r => r.GetArtifactPipelinePolicy() == policy);

        var controller = new AdminBackupController(
            trigger.Object,
            Mock.Of<IBackupRunQueryService>(),
            Mock.Of<IBackupRunService>(),
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
            Mock.Of<IPitrService>(),
            Mock.Of<IBackupVerificationReportService>(),
            Mock.Of<ICurrentTenantAccessor>(a => a.TenantId == tenantId));

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "actor-1"),
                    new Claim(ClaimTypes.Role, role),
                },
                "Test")),
        };
        http.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "corr-trigger";
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, trigger);
    }

    [Fact]
    public async Task Trigger_Manager_WithoutTenantContext_Returns400_AndDoesNotEnqueue()
    {
        await using var db = CreateDb();
        var (controller, trigger) = CreateController(db, Roles.Manager, tenantId: null);

        var result = await controller.TriggerManual(null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        trigger.Verify(
            t => t.RequestManualBackupAsync(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Trigger_Manager_WithTenantContext_Enqueues()
    {
        await using var db = CreateDb();
        var (controller, trigger) = CreateController(db, Roles.Manager, tenantId: Guid.NewGuid());

        var result = await controller.TriggerManual(null, CancellationToken.None);

        Assert.IsType<AcceptedAtActionResult>(result.Result);
        trigger.Verify(
            t => t.RequestManualBackupAsync(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Trigger_SuperAdmin_WithoutTenantContext_Enqueues_DeploymentWide()
    {
        await using var db = CreateDb();
        var (controller, trigger) = CreateController(db, Roles.SuperAdmin, tenantId: null);

        var result = await controller.TriggerManual(null, CancellationToken.None);

        Assert.IsType<AcceptedAtActionResult>(result.Result);
        trigger.Verify(
            t => t.RequestManualBackupAsync(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
