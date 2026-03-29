using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupAutomaticRetryCoordinatorTests
{
    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options;
        return new AppDbContext(options);
    }

    private static BackupOptions RetryOpts(int max, bool allowVerificationRetry = false) => new()
    {
        AutomaticRetryMaxAttempts = max,
        AutomaticRetryInitialDelay = TimeSpan.FromSeconds(5),
        AllowAutomaticRetryAfterVerificationIntegrityFailure = allowVerificationRetry
    };

    [Fact]
    public async Task Retryable_pg_dump_timeout_schedules_next_retry_within_budget()
    {
        await using var db = CreateDb($"retry_sched_{Guid.NewGuid():N}");
        var run = new BackupRun
        {
            Status = BackupRunStatus.Failed,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow,
            QueuedAt = DateTime.UtcNow,
            FailureCode = "PG_DUMP_TIMEOUT",
            FailureDetail = "timeout",
            CompletedAt = DateTime.UtcNow
        };
        BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        await BackupAutomaticRetryCoordinator.TrySchedulePendingRetryAfterTerminalSaveAsync(
            db,
            run,
            RetryOpts(max: 2),
            new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc),
            NullLogger.Instance,
            default);

        var reloaded = await db.BackupRuns.AsNoTracking().SingleAsync();
        Assert.NotNull(reloaded.NextRetryAtUtc);
        Assert.Equal("PG_DUMP_TIMEOUT", reloaded.LastRecordedTerminalFailureCode);
    }

    [Fact]
    public async Task Non_retryable_external_archive_does_not_schedule()
    {
        await using var db = CreateDb($"retry_ext_{Guid.NewGuid():N}");
        var run = new BackupRun
        {
            Status = BackupRunStatus.VerificationFailed,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow,
            FailureCode = "EXTERNAL_ARCHIVE_FAILED",
            FailureDetail = "copy failed",
            CompletedAt = DateTime.UtcNow
        };
        BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        await BackupAutomaticRetryCoordinator.TrySchedulePendingRetryAfterTerminalSaveAsync(
            db,
            run,
            RetryOpts(max: 3),
            DateTime.UtcNow,
            NullLogger.Instance,
            default);

        var reloaded = await db.BackupRuns.AsNoTracking().SingleAsync();
        Assert.Null(reloaded.NextRetryAtUtc);
    }

    [Fact]
    public async Task Retry_budget_exhausted_does_not_schedule()
    {
        await using var db = CreateDb($"retry_budget_{Guid.NewGuid():N}");
        var run = new BackupRun
        {
            Status = BackupRunStatus.Failed,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow,
            AutomaticRetryCount = 2,
            FailureCode = "PG_DUMP_FAILED",
            FailureDetail = "x",
            CompletedAt = DateTime.UtcNow
        };
        BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        await BackupAutomaticRetryCoordinator.TrySchedulePendingRetryAfterTerminalSaveAsync(
            db,
            run,
            RetryOpts(max: 2),
            DateTime.UtcNow,
            NullLogger.Instance,
            default);

        Assert.Null((await db.BackupRuns.AsNoTracking().SingleAsync()).NextRetryAtUtc);
    }

    [Fact]
    public void Verification_integrity_mismatch_not_retryable_unless_policy_enabled()
    {
        Assert.False(BackupFailureRetryClassifier.IsEligibleForAutomaticRetrySchedule(
            BackupRunStatus.VerificationFailed,
            "VERIFICATION_FAILED",
            allowVerificationIntegrityRetry: false));
        Assert.True(BackupFailureRetryClassifier.IsEligibleForAutomaticRetrySchedule(
            BackupRunStatus.VerificationFailed,
            "VERIFICATION_FAILED",
            allowVerificationIntegrityRetry: true));
    }

    [Fact]
    public async Task Process_due_requeues_clears_artifacts_and_increments_count()
    {
        await using var db = CreateDb($"retry_process_{Guid.NewGuid():N}");
        var runId = Guid.NewGuid();
        var run = new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Failed,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = DateTime.UtcNow.AddHours(-1),
            FailureCode = "PG_DUMP_TIMEOUT",
            CompletedAt = DateTime.UtcNow,
            NextRetryAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastRecordedTerminalFailureCode = "PG_DUMP_TIMEOUT"
        };
        db.BackupRuns.Add(run);
        db.BackupArtifacts.Add(new BackupArtifact
        {
            BackupRunId = runId,
            ArtifactType = BackupArtifactType.LogicalDump,
            StorageDescriptor = "x",
            CreatedAt = DateTime.UtcNow,
            LifecycleState = BackupArtifactLifecycleState.Staging
        });
        await db.SaveChangesAsync();

        var ok = await BackupAutomaticRetryCoordinator.TryProcessOneDueAutomaticRetryAsync(
            db,
            RetryOpts(max: 2),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            NullLogger.Instance,
            default);

        Assert.True(ok);
        var reloaded = await db.BackupRuns.AsNoTracking().SingleAsync();
        Assert.Equal(BackupRunStatus.Queued, reloaded.Status);
        Assert.Equal(1, reloaded.AutomaticRetryCount);
        Assert.Null(reloaded.NextRetryAtUtc);
        Assert.Equal(0, await db.BackupArtifacts.CountAsync());
    }

    [Fact]
    public void Classifier_pg_dump_failed_is_retryable()
    {
        Assert.True(BackupFailureRetryClassifier.IsEligibleForAutomaticRetrySchedule(
            BackupRunStatus.Failed,
            "PG_DUMP_FAILED",
            false));
    }

    [Fact]
    public async Task Automatic_retry_disabled_when_max_attempts_zero()
    {
        await using var db = CreateDb($"retry_off_{Guid.NewGuid():N}");
        var run = new BackupRun
        {
            Status = BackupRunStatus.Failed,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow,
            FailureCode = "PG_DUMP_TIMEOUT",
            CompletedAt = DateTime.UtcNow
        };
        BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        await BackupAutomaticRetryCoordinator.TrySchedulePendingRetryAfterTerminalSaveAsync(
            db,
            run,
            RetryOpts(max: 0),
            DateTime.UtcNow,
            NullLogger.Instance,
            default);

        Assert.Null((await db.BackupRuns.AsNoTracking().SingleAsync()).NextRetryAtUtc);
    }

    [Fact]
    public async Task Verification_failed_schedules_when_integrity_retry_allowed()
    {
        await using var db = CreateDb($"retry_ver_{Guid.NewGuid():N}");
        var run = new BackupRun
        {
            Status = BackupRunStatus.VerificationFailed,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow,
            FailureCode = "VERIFICATION_FAILED",
            FailureDetail = "hash",
            CompletedAt = DateTime.UtcNow
        };
        BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        await BackupAutomaticRetryCoordinator.TrySchedulePendingRetryAfterTerminalSaveAsync(
            db,
            run,
            RetryOpts(max: 1, allowVerificationRetry: true),
            new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc),
            NullLogger.Instance,
            default);

        Assert.NotNull((await db.BackupRuns.AsNoTracking().SingleAsync()).NextRetryAtUtc);
    }
}
