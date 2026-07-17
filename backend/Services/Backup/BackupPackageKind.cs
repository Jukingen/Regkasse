namespace KasseAPI_Final.Services.Backup;

/// <summary>Tenant package shape within <see cref="Models.Backup.BackupStrategyKind.Tenant"/>.</summary>
public enum BackupPackageKind
{
    /// <summary>Full tenant JSON ZIP (default).</summary>
    Full = 0,

    /// <summary>
    /// Delta since a prior full tenant backup watermark — smaller ZIP; not a standalone restore source.
    /// </summary>
    Incremental = 1,
}
