using System.Globalization;
using System.Text.Json;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;

namespace KasseAPI_Final.Services.Countries.Strategies.Germany;

/// <summary>
/// SIGN DE <c>standard_v1.receipt.amounts_per_vat_rate[].vat_rate</c> named enum
/// (fiskaly SIGN DE OpenAPI 2.2.2). Do not emit deprecated decimal strings.
/// </summary>
public static class DeVatRateNames
{
    public const string Normal = "NORMAL";
    public const string Reduced1 = "REDUCED_1";
    public const string Null = "NULL";
}

/// <summary>One SIGN DE <c>amounts_per_vat_rate</c> entry (gross amount, named vat_rate).</summary>
public sealed record DeAmountPerVatRate(string VatRate, string Amount);

/// <summary>
/// DE fiscal tax projection for KassenSicherheit Finish (Paket 72).
/// Distinct from AT <c>RksvTaxSetAmounts</c> — do not adapt between them.
/// </summary>
public sealed record DeFiscalTaxProjection(IReadOnlyList<DeAmountPerVatRate> AmountsPerVatRate);

/// <summary>
/// Maps DE line percents / CalculateTax tax_details to SIGN DE vat_rate buckets.
/// </summary>
public static class DeTaxSetMapper
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static DeFiscalTaxProjection MapFromLinePercents(
        IEnumerable<(decimal VatPercent, decimal GrossAmount)> lines) =>
        MapFromLinePercents(lines, DefaultCatalog);

    public static DeFiscalTaxProjection MapFromLinePercents(
        IEnumerable<(decimal VatPercent, decimal GrossAmount)> lines,
        IReadOnlyList<CountryTaxType> catalog)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(catalog);

        var buckets = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (vatPercent, grossAmount) in lines)
        {
            var vatRate = VatPercentToEnum(vatPercent, catalog);
            var gross = Round2(grossAmount);
            if (gross == 0m)
                continue;

            buckets[vatRate] = buckets.TryGetValue(vatRate, out var current)
                ? current + gross
                : gross;
        }

        return ToProjection(buckets);
    }

    public static DeFiscalTaxProjection MapFromTaxDetailsJson(string? taxDetailsJson, decimal totalGross) =>
        MapFromTaxDetailsJson(taxDetailsJson, totalGross, DefaultCatalog);

    public static DeFiscalTaxProjection MapFromTaxDetailsJson(
        string? taxDetailsJson,
        decimal totalGross,
        IReadOnlyList<CountryTaxType> catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(taxDetailsJson) || taxDetailsJson == "{}")
            return EmptyOrZeroReceipt(totalGross);

        Dictionary<string, decimal> taxByCode;
        try
        {
            using var doc = JsonDocument.Parse(taxDetailsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException(
                    "DE tax_details must be a JSON object of CountryTaxType code → tax amount.",
                    nameof(taxDetailsJson));
            }

            taxByCode = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Number)
                {
                    throw new ArgumentException(
                        $"DE tax_details value for '{prop.Name}' must be a number.",
                        nameof(taxDetailsJson));
                }

                taxByCode[prop.Name] = prop.Value.GetDecimal();
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                "DE tax_details JSON is invalid.",
                nameof(taxDetailsJson),
                ex);
        }

        if (taxByCode.Count == 0)
            return EmptyOrZeroReceipt(totalGross);

        var buckets = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (code, taxAmount) in taxByCode)
        {
            var (vatRate, ratePercent) = CodeToVatRate(code, catalog);
            var gross = ratePercent == 0m
                ? Round2(totalGross)
                : TaxToGross(taxAmount, ratePercent);

            if (gross == 0m)
                continue;

            buckets[vatRate] = buckets.TryGetValue(vatRate, out var current)
                ? current + gross
                : gross;
        }

        return ToProjection(buckets);
    }

    private static DeFiscalTaxProjection EmptyOrZeroReceipt(decimal totalGross)
    {
        // Zero / empty receipts still need a NULL bucket for SIGN DE.
        _ = totalGross;
        return new DeFiscalTaxProjection(
        [
            new DeAmountPerVatRate(DeVatRateNames.Null, FormatAmount(0m)),
        ]);
    }

    private static DeFiscalTaxProjection ToProjection(Dictionary<string, decimal> buckets)
    {
        if (buckets.Count == 0)
        {
            return new DeFiscalTaxProjection(
            [
                new DeAmountPerVatRate(DeVatRateNames.Null, FormatAmount(0m)),
            ]);
        }

        // Stable order: NORMAL, REDUCED_1, NULL (only present non-zero).
        var ordered = new List<DeAmountPerVatRate>(buckets.Count);
        foreach (var name in new[] { DeVatRateNames.Normal, DeVatRateNames.Reduced1, DeVatRateNames.Null })
        {
            if (!buckets.TryGetValue(name, out var gross) || gross == 0m)
                continue;
            ordered.Add(new DeAmountPerVatRate(name, FormatAmount(Round2(gross))));
        }

        // Any unexpected key (should not happen) — fail closed.
        foreach (var key in buckets.Keys)
        {
            if (key is not (DeVatRateNames.Normal or DeVatRateNames.Reduced1 or DeVatRateNames.Null))
            {
                throw new ArgumentException(
                    $"Unexpected DE vat_rate bucket '{key}'. Only NORMAL, REDUCED_1, and NULL are supported.");
            }
        }

        if (ordered.Count == 0)
        {
            return new DeFiscalTaxProjection(
            [
                new DeAmountPerVatRate(DeVatRateNames.Null, FormatAmount(0m)),
            ]);
        }

        return new DeFiscalTaxProjection(ordered);
    }

    private static readonly IReadOnlyList<CountryTaxType> DefaultCatalog =
        new CountryTaxTypeRegistry().Get(CountryProfileCodes.Germany);

    private static string VatPercentToEnum(decimal vatPercent, IReadOnlyList<CountryTaxType> catalog)
    {
        var row = catalog.FirstOrDefault(type => type.Rate == vatPercent);
        if (row is not null)
            return CodeToNamedRate(row.Code);

        if (vatPercent == 0m)
            return DeVatRateNames.Null;

        throw new ArgumentException(
            $"VAT percent {vatPercent.ToString(Invariant)} is not a DE CountryTaxType rate.");
    }

    private static (string VatRate, decimal RatePercent) CodeToVatRate(
        string code,
        IReadOnlyList<CountryTaxType> catalog)
    {
        var row = catalog.FirstOrDefault(type =>
            string.Equals(type.Code, code, StringComparison.OrdinalIgnoreCase));
        if (row is not null)
            return (CodeToNamedRate(row.Code), row.Rate);

        if (string.Equals(code, CountryTaxTypeCodes.Zero, StringComparison.OrdinalIgnoreCase))
            return (DeVatRateNames.Null, 0m);

        throw new ArgumentException(
            $"Unknown DE tax_details code '{code}'. Expected a DE CountryTaxType code.");
    }

    private static string CodeToNamedRate(string code)
    {
        if (string.Equals(code, CountryTaxTypeCodes.Standard, StringComparison.OrdinalIgnoreCase))
            return DeVatRateNames.Normal;
        if (string.Equals(code, CountryTaxTypeCodes.Reduced1, StringComparison.OrdinalIgnoreCase))
            return DeVatRateNames.Reduced1;
        if (string.Equals(code, CountryTaxTypeCodes.Zero, StringComparison.OrdinalIgnoreCase))
            return DeVatRateNames.Null;

        throw new ArgumentException($"DE CountryTaxType '{code}' has no SIGN DE vat_rate name.");
    }

    private static decimal TaxToGross(decimal taxAmount, decimal ratePercent)
    {
        if (taxAmount == 0m)
            return 0m;
        return Round2(taxAmount * (100m + ratePercent) / ratePercent);
    }

    private static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string FormatAmount(decimal value) =>
        Round2(value).ToString("0.00", Invariant);
}
