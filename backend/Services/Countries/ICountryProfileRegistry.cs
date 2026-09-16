using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// In-code registry of <see cref="CountryProfile"/> seeds. Read-only: profiles are a code/review
/// change, not an ops configuration edit.
/// </summary>
public interface ICountryProfileRegistry
{
    /// <summary>Every seeded profile, including registry-only fallbacks.</summary>
    IReadOnlyList<CountryProfile> All { get; }

    /// <summary>Profiles offerable in the create-tenant country list (excludes <c>EU_DEFAULT</c>).</summary>
    IReadOnlyList<CountryProfile> TenantSelectable { get; }

    /// <summary>Austria — the production fiscal path and the fallback for unknown or legacy values.</summary>
    CountryProfile Default { get; }

    /// <summary>Case-insensitive lookup. False when the code is unknown.</summary>
    bool TryGet(string? code, out CountryProfile profile);

    /// <summary>
    /// Lookup for resolving an existing tenant. Unknown, legacy, or blank values resolve to
    /// <see cref="Default"/> (Austria), per <c>docs/COUNTRIES.md</c> §8.
    /// Do not use this to validate operator input — use <see cref="Get"/>.
    /// </summary>
    CountryProfile GetOrDefault(string? code);

    /// <summary>
    /// Strict lookup for operator input (provisioning, admin APIs).
    /// </summary>
    /// <exception cref="UnknownCountryCodeException">The code is not seeded.</exception>
    CountryProfile Get(string? code);
}

/// <summary>Raised when a country code is not present in the registry.</summary>
public sealed class UnknownCountryCodeException : Exception
{
    public const string Code = "UNKNOWN_COUNTRY_CODE";

    public UnknownCountryCodeException(string? countryCode)
        : base($"Unknown country code '{countryCode}'. Supported codes are seeded in CountryProfileRegistry.")
    {
        CountryCode = countryCode;
    }

    public string? CountryCode { get; }

    /// <summary>Stable error code for API responses.</summary>
    public string ErrorCode => Code;
}
