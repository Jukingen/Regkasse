using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Lightweight change inventory for incremental tenant backups (counts only).</summary>
public sealed class IncrementalChangeSummary
{
    public Guid TenantId { get; init; }
    public DateTime SinceUtc { get; init; }
    public IReadOnlyDictionary<string, int> TableChangeCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public int TotalChangedRows => TableChangeCounts.Values.Sum();
}

public interface IIncrementalBackupService
{
    /// <summary>
    /// Counts tenant rows changed since <paramref name="lastFullBackupUtc"/> (preview / cost estimate).
    /// Does not write artifacts.
    /// </summary>
    Task<IncrementalChangeSummary> GetChangesSinceAsync(
        Guid tenantId,
        DateTime lastFullBackupUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Enqueues a Tenant-strategy incremental package (delta ZIP) for the worker.
    /// Not a standalone RKSV restore source — use with a prior full tenant backup / System dump for recovery.
    /// </summary>
    Task<BackupResult> CreateIncrementalBackupAsync(
        Guid tenantId,
        Guid userId,
        DateTime lastFullBackupUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Latest full Tenant package plus later incrementals (planning only).
    /// Tenant ZIP is not <c>pg_restore</c> input.
    /// </summary>
    Task<IncrementalRestorePlanDto> PlanRestoreFromIncrementalAsync(
        Guid tenantId,
        DateTime? targetUtc = null,
        CancellationToken ct = default);

    /// <summary>
    /// Plans full + incrementals and optionally enqueues an isolated System-dump restore drill.
    /// Never restores production.
    /// </summary>
    Task<IncrementalRestoreResultDto> RestoreFromIncrementalAsync(
        Guid tenantId,
        DateTime targetUtc,
        string actorUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Enqueues a Tenant incremental for each active mandant whose last full succeeded
    /// and that has changes since that watermark. Used by the daily hosted scheduler.
    /// </summary>
    Task<int> EnqueueDueDailyIncrementalsAsync(CancellationToken ct = default);
}
