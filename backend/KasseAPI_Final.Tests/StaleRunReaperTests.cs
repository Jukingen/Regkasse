using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.OperationalRuns;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class StaleRunReaperTests
{
    private static StaleRunReaperLeaseOptions DefaultLeaseOpts() =>
        new(TimeSpan.FromMinutes(30), 2.0, TimeSpan.FromMinutes(30), 2.0);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"stale_reaper_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Expired_running_backup_run_marked_failed_worker_lost()
    {
        await using var db = CreateDb();
        var past = DateTime.UtcNow.AddHours(-1);
        var run = new BackupRun
        {
            Status = BackupRunStatus.Running,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = past,
            StartedAt = past,
            LeaseExpiresAtUtc = past,
            LastHeartbeatAtUtc = past
        };
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        await StaleRunReaper.RecoverStaleRunsAsync(
            db,
            DateTime.UtcNow,
            DefaultLeaseOpts(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        await db.Entry(run).ReloadAsync();
        Assert.Equal(BackupRunStatus.Failed, run.Status);
        Assert.Equal(StaleRunRecoveryCodes.WorkerLost, run.FailureCode);
        Assert.NotNull(run.StaleRecoveredAtUtc);
        Assert.Equal(StaleRunRecoveryCodes.StaleRecoveryReasonRunning, run.StaleRecoveryReason);
    }

    [Fact]
    public async Task Running_backup_null_lease_past_grace_marked_failed()
    {
        await using var db = CreateDb();
        // Lease 30m * multiplier 2 = 60m grace; started 2h ago => stale
        var started = DateTime.UtcNow.AddHours(-2);
        var run = new BackupRun
        {
            Status = BackupRunStatus.Running,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = started,
            StartedAt = started,
            LeaseExpiresAtUtc = null,
            LastHeartbeatAtUtc = null
        };
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        await StaleRunReaper.RecoverStaleRunsAsync(
            db,
            DateTime.UtcNow,
            DefaultLeaseOpts(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        await db.Entry(run).ReloadAsync();
        Assert.Equal(BackupRunStatus.Failed, run.Status);
        Assert.Equal(StaleRunRecoveryCodes.StaleRecoveryReasonNullLeaseRunning, run.StaleRecoveryReason);
    }

    [Fact]
    public async Task Running_backup_null_lease_inside_grace_not_recovered()
    {
        await using var db = CreateDb();
        var started = DateTime.UtcNow.AddMinutes(-30);
        var run = new BackupRun
        {
            Status = BackupRunStatus.Running,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = started,
            StartedAt = started,
            LeaseExpiresAtUtc = null
        };
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        await StaleRunReaper.RecoverStaleRunsAsync(
            db,
            DateTime.UtcNow,
            DefaultLeaseOpts(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        await db.Entry(run).ReloadAsync();
        Assert.Equal(BackupRunStatus.Running, run.Status);
        Assert.Null(run.StaleRecoveredAtUtc);
    }

    [Fact]
    public async Task Expired_awaiting_verification_backup_marked_verification_failed()
    {
        await using var db = CreateDb();
        var past = DateTime.UtcNow.AddHours(-1);
        var run = new BackupRun
        {
            Status = BackupRunStatus.AwaitingVerification,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = past,
            LeaseExpiresAtUtc = past,
            LastHeartbeatAtUtc = past
        };
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        var v = new BackupVerification
        {
            BackupRunId = run.Id,
            Status = BackupVerificationStatus.Pending,
            StartedAt = past,
            VerifierSource = "test"
        };
        db.BackupVerifications.Add(v);
        await db.SaveChangesAsync();

        await StaleRunReaper.RecoverStaleRunsAsync(
            db,
            DateTime.UtcNow,
            DefaultLeaseOpts(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        await db.Entry(run).ReloadAsync();
        await db.Entry(v).ReloadAsync();
        Assert.Equal(BackupRunStatus.VerificationFailed, run.Status);
        Assert.Equal(StaleRunRecoveryCodes.VerificationWorkerLost, run.FailureCode);
        Assert.Equal(BackupVerificationStatus.Failed, v.Status);
        Assert.NotNull(v.CompletedAt);
    }

    [Fact]
    public async Task Expired_running_restore_verification_marked_failed()
    {
        await using var db = CreateDb();
        var past = DateTime.UtcNow.AddHours(-1);
        var run = new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Running,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            RequestedAt = past,
            StartedAt = past,
            LeaseExpiresAtUtc = past,
            LastHeartbeatAtUtc = past
        };
        db.RestoreVerificationRuns.Add(run);
        await db.SaveChangesAsync();

        await StaleRunReaper.RecoverStaleRunsAsync(
            db,
            DateTime.UtcNow,
            DefaultLeaseOpts(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        await db.Entry(run).ReloadAsync();
        Assert.Equal(RestoreVerificationStatus.Failed, run.Status);
        Assert.Equal(StaleRunRecoveryCodes.WorkerLost, run.FailureCode);
        Assert.NotNull(run.StaleRecoveredAtUtc);
    }

    [Fact]
    public async Task Future_lease_not_recovered()
    {
        await using var db = CreateDb();
        var future = DateTime.UtcNow.AddHours(1);
        var run = new BackupRun
        {
            Status = BackupRunStatus.Running,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = DateTime.UtcNow,
            LeaseExpiresAtUtc = future,
            LastHeartbeatAtUtc = DateTime.UtcNow
        };
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        await StaleRunReaper.RecoverStaleRunsAsync(
            db,
            DateTime.UtcNow,
            DefaultLeaseOpts(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        await db.Entry(run).ReloadAsync();
        Assert.Equal(BackupRunStatus.Running, run.Status);
        Assert.Null(run.FailureCode);
    }

    [Fact]
    public async Task Terminal_backup_runs_ignored()
    {
        await using var db = CreateDb();
        var past = DateTime.UtcNow.AddHours(-1);
        var run = new BackupRun
        {
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = past,
            CompletedAt = past,
            LeaseExpiresAtUtc = past,
            LastHeartbeatAtUtc = past
        };
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        await StaleRunReaper.RecoverStaleRunsAsync(
            db,
            DateTime.UtcNow,
            DefaultLeaseOpts(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        await db.Entry(run).ReloadAsync();
        Assert.Equal(BackupRunStatus.Succeeded, run.Status);
        Assert.Null(run.StaleRecoveredAtUtc);
    }

    [Fact]
    public async Task Observer_invoked_for_each_stale_backup_running_row()
    {
        await using var db = CreateDb();
        var past = DateTime.UtcNow.AddHours(-1);
        var run = new BackupRun
        {
            Status = BackupRunStatus.Running,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = past,
            StartedAt = past,
            LeaseExpiresAtUtc = past,
            LastHeartbeatAtUtc = past
        };
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        var observer = new Mock<IDrStaleRunRecoveryObserver>();
        await StaleRunReaper.RecoverStaleRunsAsync(
            db,
            DateTime.UtcNow,
            DefaultLeaseOpts(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            observer.Object);

        observer.Verify(o => o.OnStaleBackupRunRecovered(run.Id, "running"), Times.Once);
    }

    [Fact]
    public async Task Reapplying_reaper_on_terminal_rows_is_idempotent_no_duplicate_events()
    {
        await using var db = CreateDb();
        var past = DateTime.UtcNow.AddHours(-1);
        var run = new BackupRun
        {
            Status = BackupRunStatus.Running,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = past,
            StartedAt = past,
            LeaseExpiresAtUtc = past
        };
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        var observer = new Mock<IDrStaleRunRecoveryObserver>();
        await StaleRunReaper.RecoverStaleRunsAsync(
            db,
            DateTime.UtcNow,
            DefaultLeaseOpts(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            observer.Object);
        await StaleRunReaper.RecoverStaleRunsAsync(
            db,
            DateTime.UtcNow,
            DefaultLeaseOpts(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            observer.Object);

        observer.Verify(o => o.OnStaleBackupRunRecovered(run.Id, "running"), Times.Once);
        await db.Entry(run).ReloadAsync();
        Assert.Equal(BackupRunStatus.Failed, run.Status);
    }
}
