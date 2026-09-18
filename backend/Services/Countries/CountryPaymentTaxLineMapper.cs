using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries.Strategies;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// Builds payment-path <see cref="TaxLineItemInput"/> rows.
/// Product <c>TaxType</c> ints still carry Austrian RKSV semantics and are re-interpreted per
/// country. A product-catalog migration for DE/CH tenants is not implemented in Paket 30-c.
/// </summary>
public static class CountryPaymentTaxLineMapper
{
    public static TaxLineItemInput FromProductTaxType(
        CountryProfile profile,
        decimal unitPriceGross,
        int quantity,
        int rksvTaxType,
        ICountryTaxTypeRegistry rates)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(rates);

        if (IsAustria(profile))
            return TaxLineItemInput.FromTaxType(unitPriceGross, quantity, rksvTaxType);

        if (IsEuDefault(profile))
        {
            // TEMP: OSS uses AT rates as placeholder until real OSS table (Paket 30-d).
            return TaxLineItemInput.FromVatPercent(
                unitPriceGross,
                quantity,
                TaxTypes.GetTaxRate(rksvTaxType));
        }

        return TaxLineItemInput.FromVatPercent(
            unitPriceGross,
            quantity,
            ResolveCountryPercent(profile.Code, rksvTaxType, rates));
    }

    public static bool IsAustria(CountryProfile profile) =>
        string.Equals(profile.Code, CountryProfileCodes.Austria, StringComparison.OrdinalIgnoreCase);

    public static bool IsEuDefault(CountryProfile profile) =>
        string.Equals(profile.Code, CountryProfileCodes.EuDefault, StringComparison.OrdinalIgnoreCase);

    private static decimal ResolveCountryPercent(
        string countryCode,
        int rksvTaxType,
        ICountryTaxTypeRegistry rates)
    {
        if (rksvTaxType == TaxTypes.ZeroRate)
            return 0m;

        var catalog = rates.Get(countryCode);
        var code = rksvTaxType switch
        {
            TaxTypes.Standard => CountryTaxTypeCodes.Standard,
            TaxTypes.Reduced => CountryTaxTypeCodes.Reduced1,
            TaxTypes.Special when string.Equals(
                countryCode,
                CountryProfileCodes.Switzerland,
                StringComparison.OrdinalIgnoreCase) => CountryTaxTypeCodes.Lodging,
            TaxTypes.Special => throw new ArgumentException(
                $"RKSV tax type {rksvTaxType} (Special) has no {countryCode} CountryTaxType mapping.",
                nameof(rksvTaxType)),
            TaxTypes.ReducedNew => throw new ArgumentException(
                $"RKSV tax type {rksvTaxType} (ReducedNew) has no {countryCode} CountryTaxType mapping.",
                nameof(rksvTaxType)),
            _ => throw new ArgumentException(
                $"RKSV tax type {rksvTaxType} has no {countryCode} CountryTaxType mapping.",
                nameof(rksvTaxType)),
        };

        var row = catalog.FirstOrDefault(t => t.Code == code)
            ?? throw new ArgumentException(
                $"CountryTaxType '{code}' is not seeded for {countryCode}.",
                nameof(rksvTaxType));
        return row.Rate;
    }
}
