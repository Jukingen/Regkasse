using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Başarısız yedek satırları için sınırlı otomatik yeniden kuyruğa alma: allowlist kodlar, üstel gecikme, gözlemlenebilirlik alanları.
/// </summary>
public static class BackupAutomaticRetryCoordinator
{
    private static string? TruncateCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        var t = code.Trim();
        return t.Length <= 100 ? t : t[..100];
    }

    /// <summary>Terminal başarısızlık / iptal sonrası <see cref="BackupRun.LastRecordedTerminalFailureCode"/> güncellenir.</summary>
    public static void RecordTerminalFailureObservability(BackupRun run)
    {
        if (run.Status is BackupRunStatus.Failed or BackupRunStatus.VerificationFailed or BackupRunStatus.Cancelled)
            run.LastRecordedTerminalFailureCode = TruncateCode(run.FailureCode);
    }

    public static void ClearAutomaticRetryPlanningFieldsOnSuccess(BackupRun run)
    {
        run.NextRetryAtUtc = null;
        run.LastRecordedTerminalFailureCode = null;
    }

    public static async Task TrySchedulePendingRetryAfterTerminalSaveAsync(
        AppDbContext db,
        BackupRun run,
        BackupOptions options,
        DateTime utcNow,
        ILogger logger,
        CancellationToken ct)
    {
        if (options.AutomaticRetryMaxAttempts <= 0)
            return;

        if (run.Status is not BackupRunStatus.Failed and not BackupRunStatus.VerificationFailed)
            return;

        if (run.AutomaticRetryCount >= options.AutomaticRetryMaxAttempts)
            return;

        if (run.NextRetryAtUtc != null)
            return;

        if (!BackupFailureRetryClassifier.IsEligibleForAutomaticRetrySchedule(
                run.Status,
                run.FailureCode,
                options.AllowAutomaticRetryAfterVerificationIntegrityFailure))
            return;

        var baseDelay = options.AutomaticRetryInitialDelay < TimeSpan.FromSeconds(5)
            ? TimeSpan.FromSeconds(5)
            : options.AutomaticRetryInitialDelay;

        var shift = Math.Min(run.AutomaticRetryCount, 10);
        var scaledTicks = baseDelay.Ticks * (1L << shift);
        var cappedTicks = Math.Min(TimeSpan.FromHours(24).Ticks, scaledTicks);
        var delay = TimeSpan.FromTicks(cappedTicks);

        run.NextRetryAtUtc = utcNow.Add(delay);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Backup automatic retry scheduled: runId={RunId}, automaticRetryCountBeforeRequeue={Count}, nextRetryAtUtc={Next:o}, failureCode={Code}",
            run.Id,
            run.AutomaticRetryCount,
            run.NextRetryAtUtc,
            run.FailureCode);
    }

    /// <summary>Vadesi gelen tek bir otomatik requeue işler; işlendiyse true.</summary>
    public static async Task<bool> TryProcessOneDueAutomaticRetryAsync(
        AppDbContext db,
        BackupOptions options,
        DateTime utcNow,
        ILogger logger,
        CancellationToken ct)
    {
        if (options.AutomaticRetryMaxAttempts <= 0)
            return false;

        var run = await db.BackupRuns
            .Where(r => (r.Status == BackupRunStatus.Failed || r.Status == BackupRunStatus.VerificationFailed)
                        && r.NextRetryAtUtc != null
                        && r.NextRetryAtUtc <= utcNow)
            .OrderBy(r => r.NextRetryAtUtc)
            .FirstOrDefaultAsync(ct);

        if (run == null)
            return false;

        if (run.AutomaticRetryCount >= options.AutomaticRetryMaxAttempts)
        {
            run.NextRetryAtUtc = null;
            await db.SaveChangesAsync(ct);
            logger.LogWarning(
                "Backup automatic retry dropped (budget exhausted at schedule time): runId={RunId}, automaticRetryCount={Count}",
                run.Id,
                run.AutomaticRetryCount);
            return false;
        }

        var verifications = await db.BackupVerifications.Where(v => v.BackupRunId == run.Id).ToListAsync(ct);
        var artifacts = await db.BackupArtifacts.Where(a => a.BackupRunId == run.Id).ToListAsync(ct);
        db.BackupVerifications.RemoveRange(verifications);
        db.BackupArtifacts.RemoveRange(artifacts);

        run.AutomaticRetryCount++;
        run.Status = BackupRunStatus.Queued;
        run.QueuedAt = utcNow;
        run.NextRetryAtUtc = null;
        run.StartedAt = null;
        run.CompletedAt = null;
        run.FailureCode = null;
        run.FailureDetail = null;
        run.LeaseExpiresAtUtc = null;
        run.LastHeartbeatAtUtc = null;
        run.StaleRecoveredAtUtc = null;
        run.StaleRecoveryReason = null;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Backup automatic retry re-queued: runId={RunId}, automaticRetryCount={Count}",
            run.Id,
            run.AutomaticRetryCount);

        return true;
    }
}
