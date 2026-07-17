namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Cost-oriented GFS retention: classify backups by age and select which succeeded runs may be deleted.
/// Does not delete fiscal/audit rows — only operational backup artifacts/metadata when wired by the cleaner.
/// </summary>
public interface ISmartRetentionService
{
    /// <summary>Classify a single backup timestamp into a retention tier (sync).</summary>
    RetentionPlan CalculateRetentionPlan(DateTime backupDateUtc, DateTime? utcNow = null);

    /// <summary>Async wrapper for <see cref="CalculateRetentionPlan"/> (orchestration-friendly).</summary>
    Task<RetentionPlan> CalculateRetentionPlanAsync(
        DateTime backupDate,
        CancellationToken ct = default);

    /// <summary>
    /// Within each non-daily tier, keep the newest backup per period key; mark the rest (and all Delete-tier) for removal.
    /// Daily-tier backups are always kept.
    /// </summary>
    IReadOnlyList<Guid> SelectRunsToDelete(
        IReadOnlyList<BackupRetentionCandidate> candidates,
        DateTime? utcNow = null);
}
