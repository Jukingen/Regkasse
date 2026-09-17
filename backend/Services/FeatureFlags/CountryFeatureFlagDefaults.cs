using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.FeatureFlags;

/// <summary>
/// Country-profile defaults for fiscal / e-invoicing flags.
/// DE ZUGFeRD and XRechnung are intentionally not derived here — those builders are skeletons
/// and stay off until a tenant override (or global/config) turns them on.
/// </summary>
public static class CountryFeatureFlagDefaults
{
    public static bool IsRksvAtLocked(string canonicalName, CountryProfile? profile) =>
        canonicalName == FeatureFlagNames.FiscalRksvAt
        && profile?.FiscalSystem == FiscalSystem.RKSV_AT;

    public static bool TryGet(string canonicalName, CountryProfile? profile, out bool enabled)
    {
        enabled = false;
        if (profile is null)
            return false;

        switch (canonicalName)
        {
            case FeatureFlagNames.FiscalRksvAt:
                enabled = profile.FiscalSystem == FiscalSystem.RKSV_AT;
                return true;
            case FeatureFlagNames.FiscalKassenSicherheitDe:
                enabled = profile.FiscalSystem == FiscalSystem.KASSENSICHERHEIT_DE;
                return true;
            case FeatureFlagNames.FiscalMwstCh:
                enabled = profile.FiscalSystem == FiscalSystem.MWST_CH;
                return true;
            case FeatureFlagNames.EInvoicingQrRechnung:
                enabled = profile.EInvoicingStandards.Contains(EInvoicingStandard.QR_RECHNUNG);
                return true;
            case FeatureFlagNames.EInvoicingEn16931:
                enabled = profile.EInvoicingStandards.Contains(EInvoicingStandard.EN_16931);
                return true;
            default:
                return false;
        }
    }
}
