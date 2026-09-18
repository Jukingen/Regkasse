namespace KasseAPI_Final.Models.Countries;

/// <summary>
/// Immutable, code-seeded VAT rate for one country. Not an EF entity and not a
/// <see cref="CountryProfile"/> field — rates live here so the profile stays rate-free.
/// See <c>docs/COUNTRIES.md</c> §14. DE/CH wired via Paket 30-c; AT stays on live TaxTypes.
/// </summary>
public sealed class CountryTaxType
{
    public CountryTaxType(
        string countryCode,
        string code,
        decimal rate,
        string label,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo = null)
    {
        CountryCode = countryCode;
        Code = code;
        Rate = rate;
        Label = label;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }

    /// <summary>ISO 3166-1 alpha-2 (or a registry sentinel such as <c>EU_DEFAULT</c>).</summary>
    public string CountryCode { get; }

    /// <summary>Stable code within the country (<see cref="CountryTaxTypeCodes"/>).</summary>
    public string Code { get; }

    /// <summary>VAT rate as a percent (20 = 20 %), matching <c>TaxTypes.GetTaxRate</c>.</summary>
    public decimal Rate { get; }

    /// <summary>Admin i18n key. Not user-facing copy.</summary>
    public string Label { get; }

    public DateOnly EffectiveFrom { get; }

    /// <summary>Null means still in force.</summary>
    public DateOnly? EffectiveTo { get; }
}

/// <summary>Well-known <see cref="CountryTaxType.Code"/> values.</summary>
public static class CountryTaxTypeCodes
{
    public const string Standard = "STANDARD";
    public const string Reduced1 = "REDUCED_1";
    public const string Reduced2 = "REDUCED_2";
    public const string ReducedNew = "REDUCED_NEW";
    public const string Zero = "ZERO";
    public const string Lodging = "LODGING";
}
