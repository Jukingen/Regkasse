namespace KasseAPI_Final.Services.Countries;

/// <summary>Raised when a registry country exists but must not be chosen for a tenant.</summary>
public sealed class CountryNotSelectableException : Exception
{
    public const string Code = "COUNTRY_NOT_SELECTABLE";

    public CountryNotSelectableException(string? countryCode)
        : base($"Country code '{countryCode}' is not tenant-selectable.")
    {
        CountryCode = countryCode;
    }

    public string? CountryCode { get; }

    /// <summary>Stable error code for API responses.</summary>
    public string ErrorCode => Code;
}
