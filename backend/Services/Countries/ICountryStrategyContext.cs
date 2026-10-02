using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries.EInvoicing;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// Loads the per-tenant country binding used at strategy call sites.
/// Missing <see cref="CompanySettings"/> falls back to Austria + <see cref="VatRegime.AT_RKSV_STANDARD"/>
/// (legacy rows; <c>docs/COUNTRIES.md</c> §9). Does not silently map a known non-AT country to AT.
/// </summary>
public interface ICountryStrategyContext
{
    Task<CountryStrategyBinding> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the UBL builder for an <c>EU_DEFAULT</c> binding.
    /// Any other profile throws <see cref="EInvoicing.EInvoicingNotSupportedForCountryException"/>.
    /// </summary>
    IEn16931XmlBuilder SelectEn16931Builder(CountryStrategyBinding binding);
}

/// <summary>
/// Settings + profile + regime for one resolve. <see cref="UsedLegacyFallback"/> is true only when
/// the company-settings row was missing.
/// </summary>
public sealed record CountryStrategyBinding(
    CompanySettings Settings,
    CountryProfile Profile,
    VatRegime VatRegime,
    bool UsedLegacyFallback);
