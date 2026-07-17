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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminBackupTriggerTenantScopingTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    private static IOptionsMonitor<T> OptionsMonitor<T>(T value) where T : class
    {
        var mock = new Mock<IOptionsMonitor<T>>();
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
            Strategy = BackupStrategyKind.Tenant,
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
                It.IsAny<BackupStrategyKind?>(),
                It.IsAny<bool>(),
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
            Mock.Of<IBackupComplianceStatusService>(),
            Mock.Of<IBackupStorageCostService>(),
            Mock.Of<IPitrService>(),
            Mock.Of<IBackupVerificationReportService>(),
            Mock.Of<ICurrentTenantAccessor>(a => a.TenantId == tenantId),
            Mock.Of<IBackupRunTenantAccessService>(),
            Mock.Of<IBackupArtifactImportService>(),
            Mock.Of<IBackupTimeEstimator>());

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
                It.IsAny<BackupStrategyKind?>(),
                It.IsAny<bool>(),
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
                It.IsAny<BackupStrategyKind?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Trigger_SuperAdmin_WithoutTenantContext_Enqueues()
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
                It.IsAny<BackupStrategyKind?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
