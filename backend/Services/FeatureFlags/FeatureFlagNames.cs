namespace KasseAPI_Final.Services.FeatureFlags;

/// <summary>Canonical feature flag names (match <c>FeatureFlags</c> config keys / <c>tenant_settings</c> suffix).</summary>
public static class FeatureFlagNames
{
    public const string EnableNewPaymentFlow = "EnableNewPaymentFlow";
    public const string EnableDepExportV2 = "EnableDepExportV2";
    public const string EnableOnlineOrdersV2 = "EnableOnlineOrdersV2";
    public const string EnableAutoAusfall = "EnableAutoAusfall";

    public const string FiscalRksvAt = "Fiscal.RksvAt";
    public const string FiscalKassenSicherheitDe = "Fiscal.KassenSicherheitDe";
    public const string FiscalMwstCh = "Fiscal.MwstCh";
    public const string EInvoicingZugferd = "EInvoicing.Zugferd";
    public const string EInvoicingXRechnung = "EInvoicing.XRechnung";
    public const string EInvoicingQrRechnung = "EInvoicing.QrRechnung";
    public const string EInvoicingEn16931 = "EInvoicing.En16931";
    public const string ViesCheckEnabled = "Vies.CheckEnabled";

    /// <summary>
    /// EU Peppol transport (Paket 22). Declared so later phases can reference one name.
    /// Not a member of <see cref="All"/>: resolution, appsettings, and country defaults ignore it.
    /// </summary>
    public const string EInvoicingPeppol = "EInvoicing.Peppol";

    /// <summary>
    /// Original experimental flags. Resolution stays tenant override → global override → appsettings.
    /// Country profile is never consulted.
    /// </summary>
    public static readonly IReadOnlySet<string> Experimental =
        new HashSet<string>(StringComparer.Ordinal)
        {
            EnableNewPaymentFlow,
            EnableDepExportV2,
            EnableOnlineOrdersV2,
            EnableAutoAusfall,
        };

    public static readonly IReadOnlyList<string> All =
    [
        EnableNewPaymentFlow,
        EnableDepExportV2,
        EnableOnlineOrdersV2,
        EnableAutoAusfall,
        FiscalRksvAt,
        FiscalKassenSicherheitDe,
        FiscalMwstCh,
        EInvoicingZugferd,
        EInvoicingXRechnung,
        EInvoicingQrRechnung,
        EInvoicingEn16931,
        ViesCheckEnabled,
    ];

    /// <summary>
    /// Country-phase names that exist only as constants. Kept out of <see cref="All"/>
    /// so listing and resolution stay unchanged until a later phase opts in.
    /// </summary>
    public static readonly IReadOnlyList<string> Reserved =
    [
        EInvoicingPeppol,
    ];

    public static bool IsExperimental(string canonicalName) =>
        Experimental.Contains(canonicalName);

    /// <summary>
    /// Accepts config-style names (<c>EnableNewPaymentFlow</c>), short forms (<c>NewPaymentFlow</c>),
    /// or dotted country flags (<c>Fiscal.RksvAt</c>).
    /// </summary>
    public static string Normalize(string? featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
            return string.Empty;

        var raw = featureName.Trim();
        foreach (var known in All)
        {
            if (string.Equals(known, raw, StringComparison.OrdinalIgnoreCase))
                return known;
            if (known.StartsWith("Enable", StringComparison.Ordinal)
                && string.Equals(known["Enable".Length..], raw, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        // Dotted country-flag names must not gain an Enable* prefix when unknown.
        if (raw.Contains('.', StringComparison.Ordinal))
            return raw;

        // Preserve unknown names (future flags) with Enable* casing when possible.
        if (raw.StartsWith("Enable", StringComparison.OrdinalIgnoreCase))
            return char.ToUpperInvariant(raw[0]) + raw[1..];
        return "Enable" + char.ToUpperInvariant(raw[0]) + raw[1..];
    }

    public static string SettingsKey(string canonicalName) =>
        $"FeatureFlags:{Normalize(canonicalName)}";
}

/// <summary>Values of <see cref="FeatureFlagStatusDto.Source"/>.</summary>
public static class FeatureFlagSources
{
    public const string Config = "config";
    public const string GlobalOverride = "global_override";
    public const string TenantOverride = "tenant_override";
    public const string CountryProfile = "country_profile";
    public const string Locked = "locked";

    /// <summary>Name is in <see cref="FeatureFlagNames.Reserved"/> and the Peppol reserved-exit switch is off.</summary>
    public const string Reserved = "reserved";

    /// <summary>Peppol reserved-exit is on, and this tenant is not the configured canary.</summary>
    public const string ReservedExit = "reserved_exit";

    /// <summary>Peppol reserved-exit is on and this tenant is <c>Peppol:ReservedExit:CanaryTenantId</c>.</summary>
    public const string ReservedExitCanary = "reserved_exit_canary";
}
