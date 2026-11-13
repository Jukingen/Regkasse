using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Decides whether a succeeded backup run may be deleted by operational retention.
/// Legal hold and System 7-year RKSV retention always win over flat / GFS thinning.
/// </summary>
public static class BackupRetentionGuard
{
    public static bool CanDeleteSucceededRun(
        BackupRun run,
        BackupRetentionPolicySnapshot? legalPolicy,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (run.LegalHold)
            return false;

        if (run.LegalHoldUntilUtc is DateTime until && until > utcNow)
            return false;

        if (run.Artifacts.Any(a => a.ImmutableUntilUtc is DateTime imm && imm > utcNow))
            return false;

        if (legalPolicy is { LegalRetentionEnforced: true }
            && run.Strategy == BackupStrategyKind.System)
        {
            var years = Math.Max(
                legalPolicy.ColdRetentionYears,
                BackupRetentionPolicySettings.MinColdRetentionYears);
            var backupDate = run.CompletedAt ?? run.RequestedAt;
            if (utcNow < backupDate.AddYears(years))
                return false;
        }

        return true;
    }

    public static DateTime? ResolveRetentionExpiresAtUtc(
        BackupRun run,
        BackupRetentionPolicySnapshot? legalPolicy)
    {
        if (run.LegalHoldUntilUtc is DateTime holdUntil)
            return holdUntil;

        if (legalPolicy is { LegalRetentionEnforced: true }
            && run.Strategy == BackupStrategyKind.System)
        {
            var years = Math.Max(
                legalPolicy.ColdRetentionYears,
                BackupRetentionPolicySettings.MinColdRetentionYears);
            return (run.CompletedAt ?? run.RequestedAt).AddYears(years);
        }

        if (run.Strategy == BackupStrategyKind.Tenant)
            return (run.CompletedAt ?? run.RequestedAt).AddDays(BackupStrategyPolicy.TenantRetentionDays);

        return (run.CompletedAt ?? run.RequestedAt).AddDays(BackupStrategyPolicy.SystemRetentionDays);
    }
}

/// <summary>In-memory snapshot of the singleton retention policy row + cloud readiness.</summary>
public sealed class BackupRetentionPolicySnapshot
{
    public int HotRetentionDays { get; init; } = BackupRetentionWindows.DefaultHotDays;
    public int WarmRetentionDays { get; init; } = BackupRetentionWindows.DefaultWarmDays;
    public int ColdRetentionYears { get; init; } = BackupRetentionPolicySettings.DefaultColdRetentionYears;
    public bool ColdStorageEnabled { get; init; }
    public bool LegalRetentionEnforced { get; init; } = true;
    public CloudStorageProviderKind CloudProvider { get; init; } = CloudStorageProviderKind.Filesystem;
    public bool CloudConfigured { get; init; }
    public bool CloudFallbackToFilesystem { get; init; } = true;
    public DateTime UpdatedAtUtc { get; init; }
    public string? UpdatedByUserId { get; init; }

    public BackupRetentionWindows Windows =>
        BackupRetentionWindows.FromPolicy(HotRetentionDays, WarmRetentionDays);

    public static BackupRetentionPolicySnapshot Defaults { get; } = new();
}
