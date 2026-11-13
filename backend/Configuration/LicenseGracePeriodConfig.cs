namespace KasseAPI_Final.Configuration;

/// <summary>
/// Mandant (tenant) license grace-period policy. Deployment licenses use <see cref="DeploymentLicenseValidator"/>.
/// </summary>
public static class LicenseGracePeriodConfig
{
    /// <summary>Days after expiry during which POS remains usable (warnings only). After this, POS locks.</summary>
    public const int DefaultGracePeriodDays = 7;
    public const int DefaultWarningDaysBeforeExpiry = 14;
    public const int DefaultBlockAfterGraceDays = 0;

    /// <summary>
    /// Inclusive upper bound (days overdue) for the Locked phase after grace.
    /// Days overdue &gt; this value enter Archived (FA read-only; POS blocked).
    /// </summary>
    public const int DefaultArchiveAfterDays = 30;

    /// <summary>Days after expiry during which the tenant retains full operational access.</summary>
    public static int GracePeriodDays { get; private set; } = DefaultGracePeriodDays;

    /// <summary>Days before expiry when pre-expiry warnings begin.</summary>
    public static int WarningDaysBeforeExpiry { get; private set; } = DefaultWarningDaysBeforeExpiry;

    /// <summary>Additional days after grace before lockdown; zero means block immediately when grace ends.</summary>
    public static int BlockAfterGraceDays { get; private set; } = DefaultBlockAfterGraceDays;

    /// <summary>Days overdue after which Locked becomes Archived (default 30).</summary>
    public static int ArchiveAfterDays { get; private set; } = DefaultArchiveAfterDays;

    /// <summary>Applies <see cref="LicenseOptions"/> mandant grace settings (called at startup from DI).</summary>
    public static void ApplyFrom(LicenseOptions options)
    {
        if (options.GracePeriodDays > 0)
            GracePeriodDays = options.GracePeriodDays;

        if (options.WarningDaysBeforeExpiry > 0)
            WarningDaysBeforeExpiry = options.WarningDaysBeforeExpiry;

        if (options.BlockAfterGraceDays >= 0)
            BlockAfterGraceDays = options.BlockAfterGraceDays;

        if (options.ArchiveAfterDays > GracePeriodDays)
            ArchiveAfterDays = options.ArchiveAfterDays;
    }
}
