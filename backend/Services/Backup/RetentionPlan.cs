namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Grandfather-father-son (GFS) retention tier for a single backup artifact.
/// Yearly horizon aligns with long-term DR archive points (RKSV fiscal evidence itself lives in audit/DB, not solely in dump files).
/// </summary>
public enum RetentionTier
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Yearly = 3,
    /// <summary>Older than the yearly horizon — eligible for deletion.</summary>
    Delete = 4
}

/// <summary>Immutable retention classification for one backup timestamp.</summary>
public sealed class RetentionPlan
{
    private RetentionPlan(RetentionTier tier, DateTime backupDateUtc)
    {
        Tier = tier;
        BackupDateUtc = DateTime.SpecifyKind(backupDateUtc, DateTimeKind.Utc);
    }

    public RetentionTier Tier { get; }

    public DateTime BackupDateUtc { get; }

    public bool ShouldDelete => Tier == RetentionTier.Delete;

    public static RetentionPlan Daily(DateTime backupDate) => new(RetentionTier.Daily, backupDate);

    public static RetentionPlan Weekly(DateTime backupDate) => new(RetentionTier.Weekly, backupDate);

    public static RetentionPlan Monthly(DateTime backupDate) => new(RetentionTier.Monthly, backupDate);

    public static RetentionPlan Yearly(DateTime backupDate) => new(RetentionTier.Yearly, backupDate);

    public static RetentionPlan Delete(DateTime backupDate) => new(RetentionTier.Delete, backupDate);
}

/// <summary>Minimal input for GFS keep/delete selection (no EF dependency).</summary>
public readonly record struct BackupRetentionCandidate(Guid Id, DateTime BackupDateUtc);
