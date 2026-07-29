namespace KasseAPI_Final.Configuration;

/// <summary>
/// Global feature-flag defaults (appsettings <c>FeatureFlags</c>).
/// Tenant overrides live in <c>tenant_settings</c> via <see cref="Services.FeatureFlags.IFeatureFlagService"/>.
/// </summary>
public sealed class FeatureFlagsOptions
{
    public const string SectionName = "FeatureFlags";

    /// <summary>Experimental payment path (instrumentation / alternate flow). Default off.</summary>
    public bool EnableNewPaymentFlow { get; set; }

    /// <summary>DEP export V2 extras (schema version metadata). Default off.</summary>
    public bool EnableDepExportV2 { get; set; }

    /// <summary>Online order intake V2 markers / alternate validation. Default off.</summary>
    public bool EnableOnlineOrdersV2 { get; set; }

    /// <summary>
    /// When true (and <c>Ausfall:AutoEnqueue</c>), failover may enqueue FON Ausfall immediately.
    /// Default off — Suggested episodes only until explicitly enabled.
    /// </summary>
    public bool EnableAutoAusfall { get; set; }
}
