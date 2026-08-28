namespace KasseAPI_Final.Configuration;

/// <summary>
/// Development-only toggles to simulate dependency failures. Ignored unless the host environment is Development.
/// Configure via appsettings.Development.json (not production appsettings).
/// </summary>
public sealed class DevelopmentOptions
{
    public const string SectionName = "DevelopmentOptions";

    /// <summary>GET /api/tse/health and GET /api/pos/tse/status return Inactive/Offline while true (Development only).</summary>
    public bool SimulateTseUnavailable { get; set; }

    /// <summary>
    /// When true (Development only), skip TSE health probes and treat the device as ready.
    /// Default <c>false</c> so <c>Tse:Mode=Real</c> can sign locally. Overlay <c>TseMode=Real</c>
    /// on <c>/admin/rksv/config</c> always disables this bypass. Ignored outside Development.
    /// </summary>
    public bool BypassTseInDevelopment { get; set; }

    /// <summary>NTP fiscal guard behaves as if sync failed (Development only; requires NtpSettings.Enabled).</summary>
    public bool SimulateNtpFailure { get; set; }

    /// <summary>License snapshot is overlaid as expired (Development only).</summary>
    public bool SimulateLicenseExpired { get; set; }
}
