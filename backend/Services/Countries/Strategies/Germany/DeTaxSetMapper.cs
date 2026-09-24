using System.Globalization;
using System.Text.Json;
using KasseAPI_Final.Models.Countries;

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
        IEnumerable<(decimal VatPercent, decimal GrossAmount)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var buckets = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (vatPercent, grossAmount) in lines)
        {
            var vatRate = VatPercentToEnum(vatPercent);
            var gross = Round2(grossAmount);
            if (gross == 0m)
                continue;

            buckets[vatRate] = buckets.TryGetValue(vatRate, out var current)
                ? current + gross
                : gross;
        }

        return ToProjection(buckets);
    }

    public static DeFiscalTaxProjection MapFromTaxDetailsJson(string? taxDetailsJson, decimal totalGross)
    {
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
            var (vatRate, ratePercent) = CodeToVatRate(code);
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

    private static string VatPercentToEnum(decimal vatPercent)
    {
        if (vatPercent == 19m)
            return DeVatRateNames.Normal;
        if (vatPercent == 7m)
            return DeVatRateNames.Reduced1;
        if (vatPercent == 0m)
            return DeVatRateNames.Null;

        throw new ArgumentException(
            $"Unsupported DE VAT percent {vatPercent.ToString(Invariant)}. " +
            "SIGN DE Paket 72 allows only 19 (NORMAL), 7 (REDUCED_1), and 0 (NULL). " +
            "Rates 13, 4.9, and others are rejected.");
    }

    private static (string VatRate, decimal RatePercent) CodeToVatRate(string code)
    {
        if (string.Equals(code, CountryTaxTypeCodes.Standard, StringComparison.OrdinalIgnoreCase))
            return (DeVatRateNames.Normal, 19m);
        if (string.Equals(code, CountryTaxTypeCodes.Reduced1, StringComparison.OrdinalIgnoreCase))
            return (DeVatRateNames.Reduced1, 7m);
        if (string.Equals(code, CountryTaxTypeCodes.Zero, StringComparison.OrdinalIgnoreCase))
            return (DeVatRateNames.Null, 0m);

        if (string.Equals(code, CountryTaxTypeCodes.Reduced2, StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, CountryTaxTypeCodes.ReducedNew, StringComparison.OrdinalIgnoreCase)
            || int.TryParse(code, NumberStyles.Integer, Invariant, out _))
        {
            throw new ArgumentException(
                $"Unsupported DE tax_details code '{code}'. " +
                "Paket 72 accepts STANDARD (19%), REDUCED_1 (7%), and ZERO (0%). " +
                "AT codes / rates 13 and 4.9 are rejected.");
        }

        throw new ArgumentException(
            $"Unknown DE tax_details code '{code}'. " +
            "Expected STANDARD, REDUCED_1, or ZERO.");
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
