namespace KasseAPI_Final.Services.FeatureFlags;

/// <summary>Canonical feature flag names (match <c>FeatureFlags</c> config keys).</summary>
public static class FeatureFlagNames
{
    public const string EnableNewPaymentFlow = "EnableNewPaymentFlow";
    public const string EnableDepExportV2 = "EnableDepExportV2";
    public const string EnableOnlineOrdersV2 = "EnableOnlineOrdersV2";
    public const string EnableAutoAusfall = "EnableAutoAusfall";

    public static readonly IReadOnlyList<string> All =
    [
        EnableNewPaymentFlow,
        EnableDepExportV2,
        EnableOnlineOrdersV2,
        EnableAutoAusfall,
    ];

    /// <summary>
    /// Accepts config-style names (<c>EnableNewPaymentFlow</c>) or short forms (<c>NewPaymentFlow</c>).
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

        // Preserve unknown names (future flags) with Enable* casing when possible.
        if (raw.StartsWith("Enable", StringComparison.OrdinalIgnoreCase))
            return char.ToUpperInvariant(raw[0]) + raw[1..];
        return "Enable" + char.ToUpperInvariant(raw[0]) + raw[1..];
    }

    public static string SettingsKey(string canonicalName) =>
        $"FeatureFlags:{Normalize(canonicalName)}";
}
