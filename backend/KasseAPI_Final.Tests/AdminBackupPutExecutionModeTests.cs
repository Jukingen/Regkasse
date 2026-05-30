using System.Security.Claims;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
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
/// PUT /api/admin/backup/execution-mode — PgDump önkoşul guard, üretim Fake politikası, kalıcı kayıt + denetim günlüğü.
/// </summary>
public sealed class AdminBackupPutExecutionModeTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin_backup_exec_mode_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static IOptionsMonitor<BackupOptions> OptionsMonitor(BackupOptions value)
    {
        var mock = new Mock<IOptionsMonitor<BackupOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    private static BackupConfigurationHealthSnapshot Snap(
        BackupConfigurationHealthLevel level,
        AdminBackupRuntimeExecutionMode adminMode,
        BackupExecutionAdapterKind effective)
    {
        var opts = new BackupOptions();
        return new BackupConfigurationHealthSnapshot
        {
            Level = level,
            Issues = level == BackupConfigurationHealthLevel.Unhealthy ? new[] { "unhealthy-issue" } : Array.Empty<string>(),
            Diagnostics = Array.Empty<BackupConfigurationDiagnostic>(),
            EffectiveAdapterKind = effective,
            ConfigurationExecutionAdapterKind = opts.ExecutionAdapterKind,
            AdminRuntimeExecutionMode = adminMode,
            WorkerEnabled = true,
            RealPostgreSqlLogicalDumpConfigured = false,
            BackupExecutionReality = BackupConfigurationEvaluation.MapBackupExecutionReality(effective),
            RetentionReadiness = BackupRetentionReadinessEvaluator.Build(opts),
            ExternalArchiveReadiness = BackupExternalArchiveReadinessSnapshot.Inactive
        };
    }

    private static AdminBackupController CreateController(
        AppDbContext db,
        IBackupOperationalReadiness readiness,
        IAuditLogService audit,
        IHostEnvironment hostEnvironment,
        BackupOptions backupOptions)
    {
        var c = new AdminBackupController(
            Mock.Of<IBackupManualTriggerService>(),
            Mock.Of<IBackupRunQueryService>(),
            Mock.Of<IBackupRunService>(),
            Mock.Of<IBackupRecoverabilitySummaryService>(),
            Mock.Of<IRestoreOrchestrationBoundary>(),
            readiness,
            OptionsMonitor(backupOptions),
            Mock.Of<IBackupArtifactDownloadService>(),
            audit,
            NullLogger<AdminBackupController>.Instance,
            hostEnvironment,
            db,
            Mock.Of<IBackupSettingsAdminService>(),
            Mock.Of<IBackupDashboardStatsService>(),
            Mock.Of<IPitrService>(),
            Mock.Of<ICurrentTenantAccessor>());

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "operator-1"),
                    new Claim(ClaimTypes.Role, "SuperAdmin")
                },
                "Test"))
        };
        http.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "corr-exec-mode";
        c.ControllerContext = new ControllerContext { HttpContext = http };
        return c;
    }

    [Fact]
    public async Task Put_RealPgDump_when_preview_Unhealthy_returns_422_and_does_not_persist_or_audit()
    {
        await using var db = CreateDb();
        db.BackupRuntimeExecutionPreferences.Add(new BackupRuntimeExecutionPreference
        {
            Id = BackupRuntimeExecutionPreference.SingletonId,
            Mode = AdminBackupRuntimeExecutionMode.InheritFromConfiguration,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByUserId = "seed"
        });
        await db.SaveChangesAsync();

        var readiness = new Mock<IBackupOperationalReadiness>();
        readiness
            .Setup(x => x.GetConfigurationHealthAssumingAdminMode(AdminBackupRuntimeExecutionMode.PostgreSqlPgDump))
            .Returns(Snap(BackupConfigurationHealthLevel.Unhealthy, AdminBackupRuntimeExecutionMode.PostgreSqlPgDump, BackupExecutionAdapterKind.PgDump));

        var audit = new Mock<IAuditLogService>();

        var host = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development);
        var controller = CreateController(db, readiness.Object, audit.Object, host, new BackupOptions());

        var result = await controller.PutExecutionMode(
            new BackupExecutionModePutRequestDto { Mode = "RealPgDump" },
            CancellationToken.None);

        var obj = Assert.IsType<UnprocessableEntityObjectResult>(result.Result);
        Assert.NotNull(obj.Value);

        db.ChangeTracker.Clear();
        var row = await db.BackupRuntimeExecutionPreferences.AsNoTracking().SingleAsync();
        Assert.Equal(AdminBackupRuntimeExecutionMode.InheritFromConfiguration, row.Mode);

        audit.Verify(
            a => a.LogSystemOperationAsync(
                "BACKUP_RUNTIME_EXECUTION_MODE_CHANGED",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task Put_Fake_in_production_like_without_ack_returns_409()
    {
        await using var db = CreateDb();
        var readiness = new Mock<IBackupOperationalReadiness>();
        readiness
            .Setup(x => x.GetConfigurationHealthAssumingAdminMode(It.IsAny<AdminBackupRuntimeExecutionMode>()))
            .Returns((AdminBackupRuntimeExecutionMode m) =>
                Snap(BackupConfigurationHealthLevel.Healthy, m, BackupExecutionAdapterKind.Fake));

        var audit = new Mock<IAuditLogService>();
        var host = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production);
        var controller = CreateController(db, readiness.Object, audit.Object, host, new BackupOptions());

        var result = await controller.PutExecutionMode(
            new BackupExecutionModePutRequestDto { Mode = "Fake", ConfirmSimulatedOnlyOperationalRiskInProduction = true },
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        audit.Verify(
            a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task Put_Fake_in_production_like_with_ack_but_missing_confirm_flag_returns_400()
    {
        await using var db = CreateDb();
        var readiness = new Mock<IBackupOperationalReadiness>();
        readiness
            .Setup(x => x.GetConfigurationHealthAssumingAdminMode(It.IsAny<AdminBackupRuntimeExecutionMode>()))
            .Returns((AdminBackupRuntimeExecutionMode m) =>
                Snap(BackupConfigurationHealthLevel.Healthy, m, BackupExecutionAdapterKind.Fake));

        var audit = new Mock<IAuditLogService>();
        var host = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production);
        var opts = new BackupOptions { AcknowledgeFakeBackupAdapterOutsideDevelopment = true };
        var controller = CreateController(db, readiness.Object, audit.Object, host, opts);

        var result = await controller.PutExecutionMode(
            new BackupExecutionModePutRequestDto { Mode = "Fake", ConfirmSimulatedOnlyOperationalRiskInProduction = false },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        audit.Verify(
            a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task Put_success_persists_mode_and_emits_audit_with_correlation_id()
    {
        await using var db = CreateDb();
        var backupOpts = new BackupOptions { ExecutionAdapterKind = BackupExecutionAdapterKind.Fake };
        var readiness = new Mock<IBackupOperationalReadiness>();

        readiness
            .Setup(x => x.GetConfigurationHealthAssumingAdminMode(AdminBackupRuntimeExecutionMode.PostgreSqlPgDump))
            .Returns(Snap(BackupConfigurationHealthLevel.Healthy, AdminBackupRuntimeExecutionMode.PostgreSqlPgDump, BackupExecutionAdapterKind.PgDump));

        readiness
            .Setup(x => x.GetConfigurationHealthAssumingAdminMode(It.Is<AdminBackupRuntimeExecutionMode>(m => m != AdminBackupRuntimeExecutionMode.PostgreSqlPgDump)))
            .Returns((AdminBackupRuntimeExecutionMode m) =>
                Snap(BackupConfigurationHealthLevel.Healthy, m, BackupEffectiveExecutionAdapterResolver.ResolveEffectiveAdapterKind(backupOpts, m)));

        readiness
            .Setup(x => x.GetConfigurationHealth())
            .Returns(() =>
            {
                var row = db.BackupRuntimeExecutionPreferences.AsNoTracking()
                    .FirstOrDefault(x => x.Id == BackupRuntimeExecutionPreference.SingletonId);
                var mode = row?.Mode ?? AdminBackupRuntimeExecutionMode.InheritFromConfiguration;
                var eff = BackupEffectiveExecutionAdapterResolver.ResolveEffectiveAdapterKind(backupOpts, mode);
                return Snap(BackupConfigurationHealthLevel.Healthy, mode, eff);
            });

        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var host = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development);
        var controller = CreateController(db, readiness.Object, audit.Object, host, backupOpts);

        var result = await controller.PutExecutionMode(
            new BackupExecutionModePutRequestDto { Mode = "Fake" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(ok.Value);

        var row = await db.BackupRuntimeExecutionPreferences.AsNoTracking().SingleAsync();
        Assert.Equal(AdminBackupRuntimeExecutionMode.SimulatedFake, row.Mode);
        Assert.Equal("operator-1", row.UpdatedByUserId);

        audit.Verify(
            a => a.LogSystemOperationAsync(
                "BACKUP_RUNTIME_EXECUTION_MODE_CHANGED",
                "BackupRuntimeExecutionPreference",
                "operator-1",
                "SuperAdmin",
                It.Is<string>(d => d.Contains("SimulatedFake", StringComparison.Ordinal)),
                null,
                AuditLogStatus.Success,
                null,
                It.Is<object?>(o => o != null),
                null,
                "corr-exec-mode"),
            Times.Once);
    }
}
