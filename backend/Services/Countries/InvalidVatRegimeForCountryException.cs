using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Countries;

/// <summary>Raised when <see cref="VatRegime"/> is missing or not allowed for the chosen country.</summary>
public sealed class InvalidVatRegimeForCountryException : Exception
{
    public const string Code = "INVALID_VAT_REGIME_FOR_COUNTRY";

    public InvalidVatRegimeForCountryException(string? countryCode, VatRegime? vatRegime)
        : base($"VAT regime '{vatRegime}' is not allowed for country '{countryCode}'.")
    {
        CountryCode = countryCode;
        VatRegime = vatRegime;
    }

    public string? CountryCode { get; }

    public VatRegime? VatRegime { get; }

    /// <summary>Stable error code for API responses.</summary>
    public string ErrorCode => Code;
}
