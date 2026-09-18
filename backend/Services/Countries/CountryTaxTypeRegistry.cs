using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// Code-seeded VAT rates per country. DE/CH <c>CalculateTax</c> uses this registry (Paket 30-c);
/// AT stays on live <c>TaxTypes</c>. Not used by the Austrian TSE tax-set path.
/// See <c>docs/COUNTRIES.md</c> §14.
/// </summary>
public sealed class CountryTaxTypeRegistry : ICountryTaxTypeRegistry
{
    private static readonly DateOnly AtHistoricalFrom = new(2016, 1, 1);
    private static readonly DateOnly AtReducedNewFrom = new(2026, 1, 1);
    private static readonly DateOnly DeCurrentFrom = new(2021, 1, 1);
    private static readonly DateOnly ChCurrentFrom = new(2024, 1, 1);

    private static readonly IReadOnlyList<CountryTaxType> Seeds =
    [
        // Source: UStG §10 / live TaxTypes.Standard
        At(CountryTaxTypeCodes.Standard, 20m, AtHistoricalFrom),
        // Source: UStG §10 / live TaxTypes.Reduced
        At(CountryTaxTypeCodes.Reduced1, 10m, AtHistoricalFrom),
        // Source: UStG §10 / live TaxTypes.Special (Mittelsteuersatz)
        At(CountryTaxTypeCodes.Reduced2, 13m, AtHistoricalFrom),
        // Source: live TaxTypes.ZeroRate
        At(CountryTaxTypeCodes.Zero, 0m, AtHistoricalFrom),
        // Source: live TaxTypes.ReducedNew (Österreich 2026)
        At(CountryTaxTypeCodes.ReducedNew, 4.9m, AtReducedNewFrom),

        // Source: UStG §12 Abs. 1 (19 % from 2021-01-01 after the 2020 temporary cut)
        De(CountryTaxTypeCodes.Standard, 19m),
        // Source: UStG §12 Abs. 2
        De(CountryTaxTypeCodes.Reduced1, 7m),

        // Source: ESTV / MWSTG from 2024-01-01
        Ch(CountryTaxTypeCodes.Standard, 8.1m),
        // Source: ESTV / MWSTG reduced
        Ch(CountryTaxTypeCodes.Reduced1, 2.6m),
        // Source: ESTV Sonder satz (Beherbergung)
        Ch(CountryTaxTypeCodes.Lodging, 3.8m),
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<CountryTaxType>> ByCountry =
        Seeds
            .GroupBy(t => t.CountryCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<CountryTaxType>)g.ToArray(),
                StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<CountryTaxType> All => Seeds;

    public IReadOnlyList<CountryTaxType> Get(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return [];

        return ByCountry.TryGetValue(countryCode.Trim(), out var rows) ? rows : [];
    }

    private static CountryTaxType At(string code, decimal rate, DateOnly from) =>
        new(
            CountryProfileCodes.Austria,
            code,
            rate,
            $"countries.taxTypes.AT.{code}",
            from);

    private static CountryTaxType De(string code, decimal rate) =>
        new(
            CountryProfileCodes.Germany,
            code,
            rate,
            $"countries.taxTypes.DE.{code}",
            DeCurrentFrom);

    private static CountryTaxType Ch(string code, decimal rate) =>
        new(
            CountryProfileCodes.Switzerland,
            code,
            rate,
            $"countries.taxTypes.CH.{code}",
            ChCurrentFrom);
}
