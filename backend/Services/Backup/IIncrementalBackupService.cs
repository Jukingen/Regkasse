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
}
