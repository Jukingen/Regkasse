namespace KasseAPI_Final.Services.Countries.EInvoicing;

/// <summary>
/// Raised when an e-invoice syntax has no builder for the mandant's country.
/// This is not a statement about legal or network acceptance.
/// </summary>
public sealed class EInvoicingNotSupportedForCountryException : InvalidOperationException
{
    public const string Code = "EINVOICING_NOT_SUPPORTED";

    public EInvoicingNotSupportedForCountryException(string syntax, string? countryCode)
        : base($"{syntax} is not available for country '{countryCode ?? "unknown"}'.")
    {
        Syntax = syntax;
        CountryCode = countryCode;
    }

    public string Syntax { get; }

    public string? CountryCode { get; }

    public string ErrorCode => Code;
}
