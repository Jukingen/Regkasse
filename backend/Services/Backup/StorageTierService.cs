using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Hot (≤30d) / Warm (≤90d) / Cold (&gt;90d) storage-class classifier for operational backup artifacts.
/// Windows can be overridden from <see cref="BackupRetentionPolicySettings"/>.
/// </summary>
public sealed class StorageTierService : IStorageTierService
{
    /// <summary>Fast-access staging window (legal/ops Hot default).</summary>
    public const int HotRetentionDays = BackupRetentionWindows.DefaultHotDays;

    /// <summary>Medium-cost window before cold/archive preference.</summary>
    public const int WarmRetentionDays = BackupRetentionWindows.DefaultWarmDays;

    private readonly ILogger<StorageTierService> _logger;

    public StorageTierService(ILogger<StorageTierService> logger)
    {
        _logger = logger;
    }

    public BackupStorageTier CalculateOptimalTier(
        DateTime backupDateUtc,
        DateTime? utcNow = null,
        BackupRetentionWindows? windows = null)
    {
        var now = NormalizeUtc(utcNow ?? DateTime.UtcNow);
        var backup = NormalizeUtc(backupDateUtc);
        var w = windows ?? BackupRetentionWindows.Defaults;

        if (backup > now)
            return BackupStorageTier.Hot;

        var days = (now.Date - backup.Date).Days;

        if (days <= w.HotDays)
            return BackupStorageTier.Hot;

        if (days <= w.WarmDays)
            return BackupStorageTier.Warm;

        return BackupStorageTier.Cold;
    }

    public async Task<TierResult> MoveToOptimalTierAsync(
        AppDbContext db,
        BackupRun run,
        CancellationToken ct = default,
        DateTime? utcNow = null,
        BackupRetentionWindows? windows = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(run);

        var backupDate = TierResult.ResolveBackupDate(run);
        var tier = CalculateOptimalTier(backupDate, utcNow, windows);

        var artifacts = run.Artifacts.Count > 0
            ? run.Artifacts.ToList()
            : await db.BackupArtifacts
                .Where(a => a.BackupRunId == run.Id)
                .ToListAsync(ct);

        var updated = 0;
        foreach (var artifact in artifacts)
        {
            if (artifact.StorageTier == tier)
                continue;

            artifact.StorageTier = tier;
            updated++;
        }

        if (updated > 0)
        {
            _logger.LogInformation(
                "Backup storage tier updated: runId={RunId}, tier={Tier}, artifactsUpdated={Count}, recommendedLocation={Location}",
                run.Id,
                tier,
                updated,
                tier == BackupStorageTier.Cold ? "external-archive" : "staging");
        }

        return TierResult.FromTier(tier, run, tierChanged: updated > 0, artifactsUpdated: updated);
    }

    public async Task<int> ApplyOptimalTiersForSucceededRunsAsync(
        AppDbContext db,
        CancellationToken ct = default,
        DateTime? utcNow = null,
        BackupRetentionWindows? windows = null)
    {
        var runs = await db.BackupRuns
            .Include(r => r.Artifacts)
            .Where(r => r.Status == BackupRunStatus.Succeeded)
            .OrderBy(r => r.CompletedAt ?? r.RequestedAt)
            .ToListAsync(ct);

        var changedRuns = 0;
        foreach (var run in runs)
        {
            var result = await MoveToOptimalTierAsync(db, run, ct, utcNow, windows);
            if (result.TierChanged)
                changedRuns++;
        }

        if (changedRuns > 0)
        {
            _logger.LogInformation(
                "Backup storage tier pass: runsUpdated={Count}, policy=hot-{Hot}d/warm-{Warm}d/cold",
                changedRuns,
                HotRetentionDays,
                WarmRetentionDays);
        }

        return changedRuns;
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
