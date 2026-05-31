namespace KasseAPI_Final.Configuration;

/// <summary>
/// Mandant (tenant) license grace-period policy. Deployment licenses use <see cref="DeploymentLicenseValidator"/>.
/// </summary>
public static class LicenseGracePeriodConfig
{
    public const int DefaultGracePeriodDays = 21;
    public const int DefaultWarningDaysBeforeExpiry = 14;
    public const int DefaultBlockAfterGraceDays = 0;

    /// <summary>Days after expiry during which the tenant retains full operational access.</summary>
    public static int GracePeriodDays { get; private set; } = DefaultGracePeriodDays;

    /// <summary>Days before expiry when pre-expiry warnings begin.</summary>
    public static int WarningDaysBeforeExpiry { get; private set; } = DefaultWarningDaysBeforeExpiry;

    /// <summary>Additional days after grace before lockdown; zero means block immediately when grace ends.</summary>
    public static int BlockAfterGraceDays { get; private set; } = DefaultBlockAfterGraceDays;

    /// <summary>Applies <see cref="LicenseOptions"/> mandant grace settings (called at startup from DI).</summary>
    public static void ApplyFrom(LicenseOptions options)
    {
        if (options.GracePeriodDays > 0)
            GracePeriodDays = options.GracePeriodDays;

        if (options.WarningDaysBeforeExpiry > 0)
            WarningDaysBeforeExpiry = options.WarningDaysBeforeExpiry;

        if (options.BlockAfterGraceDays >= 0)
            BlockAfterGraceDays = options.BlockAfterGraceDays;
    }
}
