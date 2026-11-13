using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// In-code registry of <see cref="CountryTaxType"/> seeds. Read-only: rates are a code/review
/// change, not an ops configuration edit and not a <c>tax_groups</c> row.
/// Unknown, blank, and <c>EU_DEFAULT</c> return an empty list — never Austrian rates.
/// </summary>
public interface ICountryTaxTypeRegistry
{
    /// <summary>Every seeded rate row (AT, DE, CH). Does not include empty countries.</summary>
    IReadOnlyList<CountryTaxType> All { get; }

    /// <summary>
    /// Case-insensitive, trimmed lookup. Unknown, null, blank, and <c>EU_DEFAULT</c>
    /// return an empty list. Does not fall back to Austria.
    /// </summary>
    IReadOnlyList<CountryTaxType> Get(string? countryCode);
}
