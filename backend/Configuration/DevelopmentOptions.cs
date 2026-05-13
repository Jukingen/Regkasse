namespace KasseAPI_Final.Configuration;

/// <summary>
/// Development-only toggles to simulate dependency failures. Ignored unless the host environment is Development.
/// Configure via appsettings.Development.json (not production appsettings).
/// </summary>
public sealed class DevelopmentOptions
{
    public const string SectionName = "DevelopmentOptions";

    /// <summary>GET /api/tse/health returns Offline while true (Development only).</summary>
    public bool SimulateTseUnavailable { get; set; }

    /// <summary>NTP fiscal guard behaves as if sync failed (Development only; requires NtpSettings.Enabled).</summary>
    public bool SimulateNtpFailure { get; set; }

    /// <summary>License snapshot is overlaid as expired (Development only).</summary>
    public bool SimulateLicenseExpired { get; set; }
}
