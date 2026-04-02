using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupScheduledEnqueueServiceTests
{
    private sealed class FixedUtcTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedUtcTimeProvider(DateTime utcInstant)
        {
            _utcNow = new DateTimeOffset(DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc));
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static IOptionsMonitor<BackupOptions> OptionsMonitor(BackupOptions value)
    {
        var mock = new Mock<IOptionsMonitor<BackupOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    private static IBackupOperationalReadiness HealthyReadiness()
    {
        var m = new Mock<IBackupOperationalReadiness>();
        m.Setup(x => x.GetConfigurationHealth()).Returns(new BackupConfigurationHealthSnapshot
        {
            Level = BackupConfigurationHealthLevel.Healthy,
            WorkerEnabled = true,
            EffectiveAdapterKind = BackupExecutionAdapterKind.Fake,
            ConfigurationExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
            AdminRuntimeExecutionMode = AdminBackupRuntimeExecutionMode.InheritFromConfiguration
        });
        return m.Object;
    }

    private static IBackupOperationalReadiness UnhealthyReadiness()
    {
        var m = new Mock<IBackupOperationalReadiness>();
        m.Setup(x => x.GetConfigurationHealth()).Returns(new BackupConfigurationHealthSnapshot
        {
            Level = BackupConfigurationHealthLevel.Unhealthy,
            Issues = new[] { "test unhealthy" },
            WorkerEnabled = true,
            EffectiveAdapterKind = BackupExecutionAdapterKind.Fake,
            ConfigurationExecutionAdapterKind = BackupExecutionAdapterKind.Fake,
            AdminRuntimeExecutionMode = AdminBackupRuntimeExecutionMode.InheritFromConfiguration
        });
        return m.Object;
    }

    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    private static BackupScheduledEnqueueService Sut(
        AppDbContext db,
        BackupOptions backupOptions,
        TimeProvider time,
        IBackupOperationalReadiness? readiness = null) =>
        new(
            OptionsMonitor(backupOptions),
            readiness ?? HealthyReadiness(),
            time,
            NullLogger<BackupScheduledEnqueueService>.Instance);

    [Fact]
    public async Task Enqueues_scheduled_run_when_cron_is_due()
    {
        await using var db = CreateDb($"sched_enqueue_due_{Guid.NewGuid():N}");
        var utcNow = new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc);
        var opts = new BackupOptions
        {
            ScheduledBackupEnabled = true,
            WorkerEnabled = true,
            ScheduledBackupCron = "* * * * *",
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake
        };
        var svc = Sut(db, opts, new FixedUtcTimeProvider(utcNow));

        var enqueued = await svc.TryEnqueueIfDueAsync(db);

        Assert.True(enqueued);
        var run = await db.BackupRuns.SingleAsync();
        Assert.Equal(BackupTriggerSource.Scheduled, run.TriggerSource);
        Assert.Equal(BackupRunStatus.Queued, run.Status);
        Assert.Equal(BackupExecutionAdapterKind.Fake.ToString(), run.AdapterKind);
        Assert.NotNull(run.ConfigSnapshotJson);
        Assert.Contains("backup_scheduled_enqueue", run.ConfigSnapshotJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Does_not_enqueue_second_scheduled_while_active_scheduled_exists()
    {
        await using var db = CreateDb($"sched_dup_{Guid.NewGuid():N}");
        var utcNow = new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc);
        db.BackupRuns.Add(new BackupRun
        {
            Status = BackupRunStatus.Queued,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "Fake",
            RequestedAt = utcNow.AddMinutes(-5),
            QueuedAt = utcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        var opts = new BackupOptions
        {
            ScheduledBackupEnabled = true,
            WorkerEnabled = true,
            ScheduledBackupCron = "* * * * *",
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake
        };
        var svc = Sut(db, opts, new FixedUtcTimeProvider(utcNow));

        var enqueued = await svc.TryEnqueueIfDueAsync(db);

        Assert.False(enqueued);
        Assert.Equal(1, await db.BackupRuns.CountAsync());
    }

    [Fact]
    public async Task Active_manual_run_does_not_block_scheduled_enqueue()
    {
        await using var db = CreateDb($"sched_manual_{Guid.NewGuid():N}");
        var utcNow = new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc);
        db.BackupRuns.Add(new BackupRun
        {
            Status = BackupRunStatus.Queued,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = utcNow.AddMinutes(-1),
            QueuedAt = utcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var opts = new BackupOptions
        {
            ScheduledBackupEnabled = true,
            WorkerEnabled = true,
            ScheduledBackupCron = "* * * * *",
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake
        };
        var svc = Sut(db, opts, new FixedUtcTimeProvider(utcNow));

        var enqueued = await svc.TryEnqueueIfDueAsync(db);

        Assert.True(enqueued);
        Assert.Equal(2, await db.BackupRuns.CountAsync());
        Assert.Contains(
            await db.BackupRuns.Select(r => r.TriggerSource).ToListAsync(),
            t => t == BackupTriggerSource.Scheduled);
    }

    [Fact]
    public async Task Unhealthy_configuration_skips_enqueue()
    {
        await using var db = CreateDb($"sched_unhealthy_{Guid.NewGuid():N}");
        var utcNow = new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc);
        var opts = new BackupOptions
        {
            ScheduledBackupEnabled = true,
            WorkerEnabled = true,
            ScheduledBackupCron = "* * * * *",
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake
        };
        var svc = Sut(db, opts, new FixedUtcTimeProvider(utcNow), UnhealthyReadiness());

        var enqueued = await svc.TryEnqueueIfDueAsync(db);

        Assert.False(enqueued);
        Assert.Equal(0, await db.BackupRuns.CountAsync());
    }

    [Fact]
    public async Task Worker_disabled_skips_enqueue()
    {
        await using var db = CreateDb($"sched_worker_off_{Guid.NewGuid():N}");
        var utcNow = new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc);
        var opts = new BackupOptions
        {
            ScheduledBackupEnabled = true,
            WorkerEnabled = false,
            ScheduledBackupCron = "* * * * *",
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake
        };
        var svc = Sut(db, opts, new FixedUtcTimeProvider(utcNow));

        var enqueued = await svc.TryEnqueueIfDueAsync(db);

        Assert.False(enqueued);
        Assert.Equal(0, await db.BackupRuns.CountAsync());
    }

    [Fact]
    public async Task Not_due_yet_skips_enqueue()
    {
        await using var db = CreateDb($"sched_not_due_{Guid.NewGuid():N}");
        var utcNow = new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc);
        db.BackupRuns.Add(new BackupRun
        {
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "Fake",
            RequestedAt = new DateTime(2026, 3, 29, 0, 0, 0, DateTimeKind.Utc),
            QueuedAt = new DateTime(2026, 3, 29, 0, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 3, 29, 0, 5, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();

        var opts = new BackupOptions
        {
            ScheduledBackupEnabled = true,
            WorkerEnabled = true,
            ScheduledBackupCron = "0 0 * * *",
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake
        };
        var svc = Sut(db, opts, new FixedUtcTimeProvider(utcNow));

        var enqueued = await svc.TryEnqueueIfDueAsync(db);

        Assert.False(enqueued);
        Assert.Equal(1, await db.BackupRuns.CountAsync());
    }

    [Fact]
    public async Task Legacy_ScheduleCronPlaceholder_used_when_ScheduledBackupCron_empty()
    {
        await using var db = CreateDb($"sched_legacy_{Guid.NewGuid():N}");
        var utcNow = new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc);
        var opts = new BackupOptions
        {
            ScheduledBackupEnabled = true,
            WorkerEnabled = true,
            ScheduledBackupCron = null,
            ScheduleCronPlaceholder = "* * * * *",
            ExecutionAdapterKind = BackupExecutionAdapterKind.Fake
        };
        var svc = Sut(db, opts, new FixedUtcTimeProvider(utcNow));

        var enqueued = await svc.TryEnqueueIfDueAsync(db);

        Assert.True(enqueued);
        Assert.Equal(BackupTriggerSource.Scheduled, (await db.BackupRuns.SingleAsync()).TriggerSource);
    }
}
