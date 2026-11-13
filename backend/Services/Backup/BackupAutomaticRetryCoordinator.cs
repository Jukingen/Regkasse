using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Başarısız yedek satırları için sınırlı otomatik yeniden kuyruğa alma: allowlist kodlar, deterministik üstel gecikme, gözlemlenebilirlik alanları.
/// </summary>
public static class BackupAutomaticRetryCoordinator
{
    private const int MaxBackoffExponentClamp = 10;

    private static readonly TimeSpan MaxRetryDelayWall = TimeSpan.FromHours(24);

    private static string? TruncateCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        var t = code.Trim();
        return t.Length <= 100 ? t : t[..100];
    }

    /// <summary>
    /// Deterministik gecikme: <c>min(24h, baseDelay × 2^min(automaticRetryCountBeforeSchedule, 10))</c>; <paramref name="options"/> içindeki taban en az 5 sn.
    /// </summary>
    public static TimeSpan ComputeDeterministicRetryDelay(BackupOptions options, int automaticRetryCountBeforeSchedule)
    {
        var baseDelay = options.AutomaticRetryInitialDelay < TimeSpan.FromSeconds(5)
            ? TimeSpan.FromSeconds(5)
            : options.AutomaticRetryInitialDelay;

        var shift = Math.Min(automaticRetryCountBeforeSchedule, MaxBackoffExponentClamp);
        var scaledTicks = baseDelay.Ticks * (1L << shift);
        var cappedTicks = Math.Min(MaxRetryDelayWall.Ticks, scaledTicks);
        return TimeSpan.FromTicks(cappedTicks);
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
        run.AutomaticRetryPendingClassifiedReason = null;
        run.AutomaticRetryLastScheduledAtUtc = null;
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
        {
            logger.LogWarning(
                "Backup automatic retry not scheduled (budget already exhausted): runId={RunId}, automaticRetryCount={Count}, maxAttempts={Max}, failureCode={Code}",
                run.Id,
                run.AutomaticRetryCount,
                options.AutomaticRetryMaxAttempts,
                run.FailureCode);
            return;
        }

        if (run.NextRetryAtUtc != null)
            return;

        if (!BackupFailureRetryClassifier.TryGetEligibleClassification(
                run.Status,
                run.FailureCode,
                options.AllowAutomaticRetryAfterVerificationIntegrityFailure,
                out var classifiedReason))
        {
            logger.LogDebug(
                "Backup automatic retry not scheduled (failure not retryable): runId={RunId}, status={Status}, failureCode={Code}",
                run.Id,
                run.Status,
                run.FailureCode);
            return;
        }

        var delay = ComputeDeterministicRetryDelay(options, run.AutomaticRetryCount);
        run.NextRetryAtUtc = utcNow.Add(delay);
        run.AutomaticRetryPendingClassifiedReason = classifiedReason;
        run.AutomaticRetryLastScheduledAtUtc = utcNow;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Backup automatic retry scheduled: runId={RunId}, automaticRetryCountBeforeRequeue={Count}, maxAttempts={Max}, nextRetryAtUtc={Next:o}, failureCode={Code}, classifiedReason={Reason}, delay={Delay}",
            run.Id,
            run.AutomaticRetryCount,
            options.AutomaticRetryMaxAttempts,
            run.NextRetryAtUtc,
            run.FailureCode,
            classifiedReason,
            delay);
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
            run.AutomaticRetryPendingClassifiedReason = null;
            await db.SaveChangesAsync(ct);
            logger.LogWarning(
                "Backup automatic retry dropped (budget exhausted at schedule time): runId={RunId}, automaticRetryCount={Count}, maxAttempts={Max}, lastFailureCode={Code}",
                run.Id,
                run.AutomaticRetryCount,
                options.AutomaticRetryMaxAttempts,
                run.FailureCode);
            return false;
        }

        var verifications = await db.BackupVerifications.Where(v => v.BackupRunId == run.Id).ToListAsync(ct);
        var artifacts = await db.BackupArtifacts.Where(a => a.BackupRunId == run.Id).ToListAsync(ct);
        db.BackupVerifications.RemoveRange(verifications);
        db.BackupArtifacts.RemoveRange(artifacts);

        var priorClassified = run.AutomaticRetryPendingClassifiedReason;
        var priorFailureCode = run.FailureCode;

        run.AutomaticRetryCount++;
        run.Status = BackupRunStatus.Queued;
        run.QueuedAt = utcNow;
        run.NextRetryAtUtc = null;
        run.AutomaticRetryPendingClassifiedReason = null;
        run.AutomaticRetryLastScheduledAtUtc = null;
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
            "Backup automatic retry re-queued: runId={RunId}, automaticRetryCount={Count}, priorFailureCode={PriorCode}, priorClassifiedReason={PriorReason}",
            run.Id,
            run.AutomaticRetryCount,
            priorFailureCode,
            priorClassified);

        return true;
    }
}
