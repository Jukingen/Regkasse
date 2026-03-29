using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Zamanlanmış restore drill sıraya alma: yalnızca Scheduled + Succeeded + CompletedAt cadence; Scheduled Queued/Running dedup.
/// </summary>
public sealed class RestoreVerificationWeeklySchedulingTests
{
    private static IOptionsMonitor<T> OptionsMonitorOf<T>(T value) where T : class, new()
    {
        var mock = new Mock<IOptionsMonitor<T>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    private static IServiceScopeFactory CreateScopeFactory(string inMemoryDbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(inMemoryDbName));
        services.AddScoped<IRestoreVerificationSchedulingQueryService, RestoreVerificationSchedulingQueryService>();
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IServiceScopeFactory>();
    }

    private static RestoreVerificationOrchestratorHostedService CreateSchedulingOrchestrator(
        IServiceScopeFactory scopeFactory,
        RestoreVerificationOptions restoreVerificationOptions)
    {
        var hostEnv = new Mock<IHostEnvironment>();
        hostEnv.Setup(h => h.EnvironmentName).Returns(Environments.Development);

        var restoreReadiness = new RestoreVerificationOperationalReadinessService(
            OptionsMonitorOf(restoreVerificationOptions),
            hostEnv.Object);

        return new RestoreVerificationOrchestratorHostedService(
            scopeFactory,
            OptionsMonitorOf(restoreVerificationOptions),
            OptionsMonitorOf(new BackupOptions()),
            hostEnv.Object,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            Mock.Of<IRestoreVerificationOrchestratorDistributedLock>(),
            Mock.Of<IPgRestoreListInspector>(),
            Mock.Of<IPgRestoreIsolatedRestoreRunner>(),
            Mock.Of<IFiscalGoLiveValidationRunner>(),
            restoreReadiness,
            Mock.Of<IRestoreVerificationOrchestratorMetrics>(),
            Mock.Of<IBackupAlertPublisher>(),
            NullLogger<RestoreVerificationOrchestratorHostedService>.Instance);
    }

    private static RestoreVerificationOptions SchedulingEnabledOptions(int cadenceDays = 7) =>
        new()
        {
            ScheduledWeeklyDrillEnabled = true,
            ScheduledProofCadenceDays = cadenceDays
        };

    [Fact]
    public async Task Scheduler_enqueues_when_last_scheduled_run_failed()
    {
        var dbName = $"rv_sched_failed_{Guid.NewGuid():N}";
        var sf = CreateScopeFactory(dbName);
        await using (var scope = sf.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RestoreVerificationRuns.Add(new RestoreVerificationRun
            {
                Status = RestoreVerificationStatus.Failed,
                TriggerSource = RestoreVerificationTriggerSource.Scheduled,
                RequestedAt = DateTime.UtcNow.AddDays(-1),
                CompletedAt = DateTime.UtcNow.AddDays(-1),
                FailureCode = "X"
            });
            await db.SaveChangesAsync();
        }

        var orchestrator = CreateSchedulingOrchestrator(sf, SchedulingEnabledOptions());
        await orchestrator.TryEnqueueWeeklyIfDueExclusiveBodyAsync(CancellationToken.None);

        await using var assertScope = sf.CreateAsyncScope();
        var dbOut = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await dbOut.RestoreVerificationRuns.AsNoTracking().OrderBy(r => r.RequestedAt).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(RestoreVerificationStatus.Queued, rows[1].Status);
        Assert.NotNull(rows[1].ConfigSnapshotJson);
        Assert.Contains("restore_scheduled_enqueue", rows[1].ConfigSnapshotJson, StringComparison.Ordinal);
        Assert.Equal(RestoreVerificationTriggerSource.Scheduled, rows[1].TriggerSource);
    }

    [Fact]
    public async Task Scheduler_skips_enqueue_when_scheduled_success_within_cadence_window()
    {
        var dbName = $"rv_sched_ok_{Guid.NewGuid():N}";
        var sf = CreateScopeFactory(dbName);
        await using (var scope = sf.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RestoreVerificationRuns.Add(new RestoreVerificationRun
            {
                Status = RestoreVerificationStatus.Succeeded,
                TriggerSource = RestoreVerificationTriggerSource.Scheduled,
                RequestedAt = DateTime.UtcNow.AddHours(-2),
                CompletedAt = DateTime.UtcNow.AddHours(-1)
            });
            await db.SaveChangesAsync();
        }

        var orchestrator = CreateSchedulingOrchestrator(sf, SchedulingEnabledOptions(cadenceDays: 7));
        await orchestrator.TryEnqueueWeeklyIfDueExclusiveBodyAsync(CancellationToken.None);

        await using var assertScope = sf.CreateAsyncScope();
        var count = await assertScope.ServiceProvider.GetRequiredService<AppDbContext>().RestoreVerificationRuns.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Scheduler_uses_completed_at_not_requested_at_for_cadence_gate()
    {
        var dbName = $"rv_sched_completed_at_{Guid.NewGuid():N}";
        var sf = CreateScopeFactory(dbName);
        var completedRecent = DateTime.UtcNow.AddHours(-1);
        await using (var scope = sf.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RestoreVerificationRuns.Add(new RestoreVerificationRun
            {
                Status = RestoreVerificationStatus.Succeeded,
                TriggerSource = RestoreVerificationTriggerSource.Scheduled,
                RequestedAt = DateTime.UtcNow.AddDays(-30),
                CompletedAt = completedRecent
            });
            await db.SaveChangesAsync();
        }

        var orchestrator = CreateSchedulingOrchestrator(sf, SchedulingEnabledOptions(cadenceDays: 7));
        await orchestrator.TryEnqueueWeeklyIfDueExclusiveBodyAsync(CancellationToken.None);

        await using var assertScope = sf.CreateAsyncScope();
        var count = await assertScope.ServiceProvider.GetRequiredService<AppDbContext>().RestoreVerificationRuns.CountAsync();
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData(RestoreVerificationStatus.Queued)]
    [InlineData(RestoreVerificationStatus.Running)]
    public async Task Scheduler_skips_enqueue_when_scheduled_active_queued_or_running_exists(RestoreVerificationStatus activeStatus)
    {
        var dbName = $"rv_sched_active_{Guid.NewGuid():N}";
        var sf = CreateScopeFactory(dbName);
        await using (var scope = sf.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RestoreVerificationRuns.Add(new RestoreVerificationRun
            {
                Status = activeStatus,
                TriggerSource = RestoreVerificationTriggerSource.Scheduled,
                RequestedAt = DateTime.UtcNow,
                StartedAt = activeStatus == RestoreVerificationStatus.Running ? DateTime.UtcNow : null
            });
            await db.SaveChangesAsync();
        }

        var orchestrator = CreateSchedulingOrchestrator(sf, SchedulingEnabledOptions());
        await orchestrator.TryEnqueueWeeklyIfDueExclusiveBodyAsync(CancellationToken.None);

        await using var assertScope = sf.CreateAsyncScope();
        var count = await assertScope.ServiceProvider.GetRequiredService<AppDbContext>().RestoreVerificationRuns.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Scheduler_enqueues_when_only_manual_success_exists_manual_never_satisfies_cadence()
    {
        var dbName = $"rv_manual_only_{Guid.NewGuid():N}";
        var sf = CreateScopeFactory(dbName);
        await using (var scope = sf.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RestoreVerificationRuns.Add(new RestoreVerificationRun
            {
                Status = RestoreVerificationStatus.Succeeded,
                TriggerSource = RestoreVerificationTriggerSource.Manual,
                RequestedAt = DateTime.UtcNow.AddHours(-2),
                CompletedAt = DateTime.UtcNow.AddHours(-1)
            });
            await db.SaveChangesAsync();
        }

        var orchestrator = CreateSchedulingOrchestrator(sf, SchedulingEnabledOptions());
        await orchestrator.TryEnqueueWeeklyIfDueExclusiveBodyAsync(CancellationToken.None);

        await using var assertScope = sf.CreateAsyncScope();
        var rows = await assertScope.ServiceProvider.GetRequiredService<AppDbContext>().RestoreVerificationRuns
            .AsNoTracking()
            .OrderBy(r => r.RequestedAt)
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(RestoreVerificationTriggerSource.Manual, rows[0].TriggerSource);
        Assert.Equal(RestoreVerificationStatus.Queued, rows[1].Status);
        Assert.Equal(RestoreVerificationTriggerSource.Scheduled, rows[1].TriggerSource);
    }

    [Fact]
    public async Task Scheduler_enqueues_despite_recent_manual_success_when_no_scheduled_proof_in_window()
    {
        var dbName = $"rv_manual_recent_{Guid.NewGuid():N}";
        var sf = CreateScopeFactory(dbName);
        await using (var scope = sf.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RestoreVerificationRuns.Add(new RestoreVerificationRun
            {
                Status = RestoreVerificationStatus.Succeeded,
                TriggerSource = RestoreVerificationTriggerSource.Manual,
                RequestedAt = DateTime.UtcNow.AddMinutes(-5),
                CompletedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await db.SaveChangesAsync();
        }

        var orchestrator = CreateSchedulingOrchestrator(sf, SchedulingEnabledOptions(cadenceDays: 7));
        await orchestrator.TryEnqueueWeeklyIfDueExclusiveBodyAsync(CancellationToken.None);

        await using var assertScope = sf.CreateAsyncScope();
        var count = await assertScope.ServiceProvider.GetRequiredService<AppDbContext>().RestoreVerificationRuns.CountAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Scheduler_skips_enqueue_when_configuration_unhealthy_and_increments_metric()
    {
        var dbName = $"rv_sched_unhealthy_{Guid.NewGuid():N}";
        var sf = CreateScopeFactory(dbName);
        var restoreOpts = SchedulingEnabledOptions();
        restoreOpts.OrchestratorPollingInterval = TimeSpan.FromMilliseconds(500);

        var hostEnv = new Mock<IHostEnvironment>();
        hostEnv.Setup(h => h.EnvironmentName).Returns(Environments.Development);
        var restoreReadiness = new RestoreVerificationOperationalReadinessService(
            OptionsMonitorOf(restoreOpts),
            hostEnv.Object);
        var metrics = new Mock<IRestoreVerificationOrchestratorMetrics>();

        var orchestrator = new RestoreVerificationOrchestratorHostedService(
            sf,
            OptionsMonitorOf(restoreOpts),
            OptionsMonitorOf(new BackupOptions()),
            hostEnv.Object,
            new ConfigurationBuilder().Build(),
            Mock.Of<IRestoreVerificationOrchestratorDistributedLock>(),
            Mock.Of<IPgRestoreListInspector>(),
            Mock.Of<IPgRestoreIsolatedRestoreRunner>(),
            Mock.Of<IFiscalGoLiveValidationRunner>(),
            restoreReadiness,
            metrics.Object,
            Mock.Of<IBackupAlertPublisher>(),
            NullLogger<RestoreVerificationOrchestratorHostedService>.Instance);

        await orchestrator.TryEnqueueWeeklyIfDueExclusiveBodyAsync(CancellationToken.None);

        await using var assertScope = sf.CreateAsyncScope();
        var count = await assertScope.ServiceProvider.GetRequiredService<AppDbContext>().RestoreVerificationRuns.CountAsync();
        Assert.Equal(0, count);
        metrics.Verify(m => m.RecordScheduledEnqueueSuppressed("unhealthy_configuration"), Times.Once);
    }
}
