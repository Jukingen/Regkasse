using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries.Strategies;

/// <summary>
/// Selects the <see cref="ITaxStrategy"/> for a country profile and VAT regime. Fails closed: an
/// unsupported pair must never silently fall back to Austria.
/// </summary>
public interface ITaxStrategyResolver
{
    /// <exception cref="UnknownTaxRegimeException">
    /// The regime is not allowed for the profile, or no strategy is registered for the country.
    /// </exception>
    ITaxStrategy Resolve(CountryProfile profile, VatRegime vatRegime);
}

/// <summary>
/// Raised when a country / VAT-regime pair has no strategy. Shared by both resolvers so API callers
/// get one stable error code.
/// </summary>
public sealed class UnknownTaxRegimeException : Exception
{
    public const string Code = "UNKNOWN_TAX_REGIME";

    public UnknownTaxRegimeException(string? countryCode, VatRegime vatRegime)
        : base($"No strategy for country '{countryCode}' with VAT regime '{vatRegime}'. "
            + "Allowed regimes are seeded per country in CountryProfileRegistry.")
    {
        CountryCode = countryCode;
        VatRegime = vatRegime;
    }

    public string? CountryCode { get; }

    public VatRegime VatRegime { get; }

    /// <summary>Stable error code for API responses.</summary>
    public string ErrorCode => Code;
}
