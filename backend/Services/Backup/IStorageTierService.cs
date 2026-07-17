using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Cost-oriented Hot / Warm / Cold classification for succeeded backup runs.
/// Does not move fiscal data; persists <see cref="BackupArtifact.StorageTier"/> when applying.
/// Cold recommends external archive (<c>Backup:ExternalArchiveRoot</c>) — no silent cloud Glacier API.
/// </summary>
public interface IStorageTierService
{
    /// <summary>Classify by age only (no persistence).</summary>
    BackupStorageTier CalculateOptimalTier(DateTime backupDateUtc, DateTime? utcNow = null);

    /// <summary>
    /// Classify a run and persist the tier on its artifacts.
    /// Uses <see cref="BackupRun.CompletedAt"/> (fallback <see cref="BackupRun.RequestedAt"/>) — not a fictional Backup table.
    /// </summary>
    Task<TierResult> MoveToOptimalTierAsync(
        AppDbContext db,
        BackupRun run,
        CancellationToken ct = default,
        DateTime? utcNow = null);

    /// <summary>Reclassify all succeeded runs that still have artifacts (post-success / ops pass).</summary>
    Task<int> ApplyOptimalTiersForSucceededRunsAsync(
        AppDbContext db,
        CancellationToken ct = default,
        DateTime? utcNow = null);
}
