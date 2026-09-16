namespace KasseAPI_Final.Configuration;

/// <summary>
/// Deployment-wide automatic Tagesabschluss fallback (per-tenant time lives on company settings).
/// </summary>
public sealed class AutoTagesabschlussOptions
{
    public const string SectionName = "AutoTagesabschluss";

    /// <summary>When false, the hosted worker does not auto-close any register.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Default Europe/Vienna hour used when a tenant has no company-settings override.</summary>
    public int DefaultHourVienna { get; set; } = 3;

    /// <summary>Default Europe/Vienna minute used when a tenant has no company-settings override.</summary>
    public int DefaultMinuteVienna { get; set; } = 0;

    /// <summary>Hosted service polling interval in minutes.</summary>
    public int CheckIntervalMinutes { get; set; } = 15;
}
