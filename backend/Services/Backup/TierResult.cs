using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Result of classifying (and optionally persisting) a run's storage tier.</summary>
public sealed class TierResult
{
    private TierResult(
        BackupStorageTier tier,
        Guid backupRunId,
        DateTime backupDateUtc,
        bool tierChanged,
        int artifactsUpdated)
    {
        Tier = tier;
        BackupRunId = backupRunId;
        BackupDateUtc = DateTime.SpecifyKind(backupDateUtc, DateTimeKind.Utc);
        TierChanged = tierChanged;
        ArtifactsUpdated = artifactsUpdated;
    }

    public BackupStorageTier Tier { get; }

    public Guid BackupRunId { get; }

    public DateTime BackupDateUtc { get; }

    /// <summary>True when at least one artifact's stored tier was updated.</summary>
    public bool TierChanged { get; }

    public int ArtifactsUpdated { get; }

    /// <summary>Operator hint: where this tier is expected to live.</summary>
    public string RecommendedLocation => Tier switch
    {
        BackupStorageTier.Hot => "staging",
        BackupStorageTier.Warm => "staging",
        BackupStorageTier.Cold => "external-archive",
        _ => "staging"
    };

    public static TierResult Hot(BackupRun run, bool tierChanged = false, int artifactsUpdated = 0) =>
        new(BackupStorageTier.Hot, run.Id, ResolveBackupDate(run), tierChanged, artifactsUpdated);

    public static TierResult Warm(BackupRun run, bool tierChanged = false, int artifactsUpdated = 0) =>
        new(BackupStorageTier.Warm, run.Id, ResolveBackupDate(run), tierChanged, artifactsUpdated);

    public static TierResult Cold(BackupRun run, bool tierChanged = false, int artifactsUpdated = 0) =>
        new(BackupStorageTier.Cold, run.Id, ResolveBackupDate(run), tierChanged, artifactsUpdated);

    public static TierResult FromTier(
        BackupStorageTier tier,
        BackupRun run,
        bool tierChanged = false,
        int artifactsUpdated = 0) =>
        tier switch
        {
            BackupStorageTier.Hot => Hot(run, tierChanged, artifactsUpdated),
            BackupStorageTier.Warm => Warm(run, tierChanged, artifactsUpdated),
            _ => Cold(run, tierChanged, artifactsUpdated)
        };

    internal static DateTime ResolveBackupDate(BackupRun run) =>
        run.CompletedAt ?? run.RequestedAt;
}
