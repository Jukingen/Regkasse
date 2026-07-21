using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.OperationalRuns;

/// <summary>
/// Süresi dolmuş lease veya null-lease + grace aşımı ile kalan backup / restore drill satırlarını terminal yapar (idempotent).
/// </summary>
public static class StaleRunReaper
{
    /// <summary>Stale sonrası otomatik retry planlaması için kurtarılan backup run kimlikleri.</summary>
    public static async Task<IReadOnlyList<Guid>> RecoverStaleRunsAsync(
        AppDbContext db,
        DateTime utcNow,
        StaleRunReaperLeaseOptions leaseOptions,
        ILogger logger,
        IDrStaleRunRecoveryObserver? observer = null,
        CancellationToken cancellationToken = default)
    {
        var backupGrace = ScaleLeaseGrace(
            leaseOptions.BackupRunLeaseTimeout,
            leaseOptions.BackupNullLeaseGraceMultiplier);
        var restoreGrace = ScaleLeaseGrace(
            leaseOptions.RestoreRunLeaseTimeout,
            leaseOptions.RestoreNullLeaseGraceMultiplier);

        var recoveredBackupIds = new List<Guid>();
        recoveredBackupIds.AddRange(
            await RecoverStaleBackupRunsRunningAsync(db, utcNow, backupGrace, logger, observer, cancellationToken)
                .ConfigureAwait(false));
        recoveredBackupIds.AddRange(
            await RecoverStaleBackupRunsAwaitingVerificationAsync(db, utcNow, backupGrace, logger, observer, cancellationToken)
                .ConfigureAwait(false));
        await RecoverStaleRestoreRunsAsync(db, utcNow, restoreGrace, logger, observer, cancellationToken)
            .ConfigureAwait(false);
        return recoveredBackupIds;
    }

    private static TimeSpan ScaleLeaseGrace(TimeSpan runLeaseTimeout, double multiplier)
    {
        var ms = runLeaseTimeout.TotalMilliseconds * multiplier;
        if (ms >= TimeSpan.MaxValue.TotalMilliseconds)
            return TimeSpan.MaxValue;
        return TimeSpan.FromMilliseconds(ms);
    }

    private static async Task<List<Guid>> RecoverStaleBackupRunsRunningAsync(
        AppDbContext db,
        DateTime utcNow,
        TimeSpan nullLeaseGrace,
        ILogger logger,
        IDrStaleRunRecoveryObserver? observer,
        CancellationToken cancellationToken)
    {
        var nullLeaseCutoff = utcNow - nullLeaseGrace;
        var rows = await db.BackupRuns
            .Where(r => r.Status == BackupRunStatus.Running
                        && (
                            (r.LeaseExpiresAtUtc != null && r.LeaseExpiresAtUtc < utcNow)
                            || (r.LeaseExpiresAtUtc == null
                                && (
                                    (r.StartedAt != null && r.StartedAt < nullLeaseCutoff)
                                    || (r.StartedAt == null && r.RequestedAt < nullLeaseCutoff)))))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
            return [];

        foreach (var r in rows)
        {
            var nullLease = r.LeaseExpiresAtUtc == null;
            r.Status = BackupRunStatus.Failed;
            r.CompletedAt = utcNow;
            r.FailureCode = StaleRunRecoveryCodes.WorkerLost;
            r.FailureDetail = nullLease
                ? StaleRunRecoveryCodes.StaleRecoveryReasonNullLeaseRunning
                : StaleRunRecoveryCodes.StaleRecoveryReasonRunning;
            r.StaleRecoveredAtUtc = utcNow;
            r.StaleRecoveryReason = nullLease
                ? StaleRunRecoveryCodes.StaleRecoveryReasonNullLeaseRunning
                : StaleRunRecoveryCodes.StaleRecoveryReasonRunning;
            BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(r);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogWarning(
            "Stale run reaper: recovered {Count} backup run(s) from Running (lease expired or null-lease grace exceeded).",
            rows.Count);
        if (observer != null)
        {
            foreach (var r in rows)
                observer.OnStaleBackupRunRecovered(r.Id, "running");
        }

        return rows.Select(r => r.Id).ToList();
    }

    private static async Task<List<Guid>> RecoverStaleBackupRunsAwaitingVerificationAsync(
        AppDbContext db,
        DateTime utcNow,
        TimeSpan nullLeaseGrace,
        ILogger logger,
        IDrStaleRunRecoveryObserver? observer,
        CancellationToken cancellationToken)
    {
        var nullLeaseCutoff = utcNow - nullLeaseGrace;
        var runs = await db.BackupRuns
            .Where(r => r.Status == BackupRunStatus.AwaitingVerification
                        && (
                            (r.LeaseExpiresAtUtc != null && r.LeaseExpiresAtUtc < utcNow)
                            || (r.LeaseExpiresAtUtc == null
                                && (
                                    (r.StartedAt != null && r.StartedAt < nullLeaseCutoff)
                                    || (r.StartedAt == null && r.RequestedAt < nullLeaseCutoff)))))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (runs.Count == 0)
            return [];

        var ids = runs.Select(r => r.Id).ToList();
        var pendingJson = JsonSerializer.Serialize(new { error = "stale_lease_awaiting_verification" });

        var verifs = await db.BackupVerifications
            .Where(v => ids.Contains(v.BackupRunId)
                        && v.Status == BackupVerificationStatus.Pending
                        && v.CompletedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var v in verifs)
        {
            v.Status = BackupVerificationStatus.Failed;
            v.CompletedAt = utcNow;
            v.FailureReason = StaleRunRecoveryCodes.StaleRecoveryReasonAwaitingVerification;
            v.DetailsJson = pendingJson;
        }

        foreach (var r in runs)
        {
            var nullLease = r.LeaseExpiresAtUtc == null;
            r.Status = BackupRunStatus.VerificationFailed;
            r.CompletedAt = utcNow;
            r.FailureCode = StaleRunRecoveryCodes.VerificationWorkerLost;
            r.FailureDetail = nullLease
                ? StaleRunRecoveryCodes.StaleRecoveryReasonNullLeaseAwaitingVerification
                : StaleRunRecoveryCodes.StaleRecoveryReasonAwaitingVerification;
            r.StaleRecoveredAtUtc = utcNow;
            r.StaleRecoveryReason = nullLease
                ? StaleRunRecoveryCodes.StaleRecoveryReasonNullLeaseAwaitingVerification
                : StaleRunRecoveryCodes.StaleRecoveryReasonAwaitingVerification;
            BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(r);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogWarning(
            "Stale run reaper: recovered {Count} backup run(s) from AwaitingVerification (lease expired or null-lease grace exceeded).",
            runs.Count);
        if (observer != null)
        {
            foreach (var r in runs)
                observer.OnStaleBackupRunRecovered(r.Id, "awaiting_verification");
        }

        return ids;
    }

    private static async Task RecoverStaleRestoreRunsAsync(
        AppDbContext db,
        DateTime utcNow,
        TimeSpan nullLeaseGrace,
        ILogger logger,
        IDrStaleRunRecoveryObserver? observer,
        CancellationToken cancellationToken)
    {
        var nullLeaseCutoff = utcNow - nullLeaseGrace;
        var rows = await db.RestoreVerificationRuns
            .Where(r => r.Status == RestoreVerificationStatus.Running
                        && (
                            (r.LeaseExpiresAtUtc != null && r.LeaseExpiresAtUtc < utcNow)
                            || (r.LeaseExpiresAtUtc == null
                                && (
                                    (r.StartedAt != null && r.StartedAt < nullLeaseCutoff)
                                    || (r.StartedAt == null && r.RequestedAt < nullLeaseCutoff)))))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
            return;

        foreach (var r in rows)
        {
            var nullLease = r.LeaseExpiresAtUtc == null;
            r.Status = RestoreVerificationStatus.Failed;
            r.CompletedAt = utcNow;
            r.FailureCode = StaleRunRecoveryCodes.WorkerLost;
            r.FailureDetail = nullLease
                ? StaleRunRecoveryCodes.StaleRecoveryReasonNullLeaseRestoreRunning
                : StaleRunRecoveryCodes.StaleRecoveryReasonRestoreRunning;
            r.StaleRecoveredAtUtc = utcNow;
            r.StaleRecoveryReason = nullLease
                ? StaleRunRecoveryCodes.StaleRecoveryReasonNullLeaseRestoreRunning
                : StaleRunRecoveryCodes.StaleRecoveryReasonRestoreRunning;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogWarning(
            "Stale run reaper: recovered {Count} restore verification run(s) from Running (lease expired or null-lease grace exceeded).",
            rows.Count);
        if (observer != null)
        {
            foreach (var r in rows)
                observer.OnStaleRestoreVerificationRunRecovered(r.Id);
        }
    }
}
