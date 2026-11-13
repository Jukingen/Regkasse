namespace KasseAPI_Final.Configuration;

/// <summary>
/// Global feature-flag defaults (appsettings <c>FeatureFlags</c>).
/// Tenant overrides live in <c>tenant_settings</c> via <see cref="Services.FeatureFlags.IFeatureFlagService"/>.
/// <c>Fiscal.RksvAt</c> is intentionally absent — it is resolved from the country profile and locked on for AT.
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

    /// <summary>Fiscal module flags. Does not include <c>Fiscal.RksvAt</c>.</summary>
    public FiscalFeatureFlagsOptions Fiscal { get; set; } = new();

    /// <summary>E-invoicing builders. DE ZUGFeRD / XRechnung stay off until those modules ship.</summary>
    public EInvoicingFeatureFlagsOptions EInvoicing { get; set; } = new();

    public ViesFeatureFlagsOptions Vies { get; set; } = new();
}

public sealed class FiscalFeatureFlagsOptions
{
    public bool KassenSicherheitDe { get; set; }

    public bool MwstCh { get; set; }
}

public sealed class EInvoicingFeatureFlagsOptions
{
    public bool Zugferd { get; set; }

    public bool XRechnung { get; set; }

    public bool QrRechnung { get; set; }

    public bool En16931 { get; set; }
}

public sealed class ViesFeatureFlagsOptions
{
    public bool CheckEnabled { get; set; }
}
