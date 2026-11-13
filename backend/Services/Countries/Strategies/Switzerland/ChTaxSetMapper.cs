using System.Text.Json;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services.Countries.Strategies.Switzerland;

/// <summary>
/// Projects persisted CH tax_details onto the shared <see cref="RksvTaxSetAmounts"/> shape.
/// Rates come only from the CH <see cref="CountryTaxType"/> catalog. This is not an RKSV bucket rule.
/// </summary>
public static class ChTaxSetMapper
{
    public static RksvTaxSetAmounts MapFromTaxDetailsJson(
        string? taxDetailsJson,
        decimal totalGross,
        IReadOnlyList<CountryTaxType> catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Count == 0)
            throw new InvalidOperationException("CH CountryTaxType catalog is empty.");

        if (string.IsNullOrWhiteSpace(taxDetailsJson) || taxDetailsJson == "{}")
            return RksvTaxSetAmounts.Zero;

        Dictionary<string, decimal> taxByCode;
        try
        {
            using var doc = JsonDocument.Parse(taxDetailsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException(
                    "CH tax_details must be a JSON object of CountryTaxType code → tax amount.",
                    nameof(taxDetailsJson));
            }

            taxByCode = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Number)
                {
                    throw new ArgumentException(
                        $"CH tax_details value for '{prop.Name}' must be a number.",
                        nameof(taxDetailsJson));
                }

                taxByCode[prop.Name] = prop.Value.GetDecimal();
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("CH tax_details JSON is invalid.", nameof(taxDetailsJson), ex);
        }

        if (taxByCode.Count == 0)
            return RksvTaxSetAmounts.Zero;

        decimal normal = 0m;
        decimal reduced1 = 0m;
        decimal reduced2 = 0m;
        decimal zero = 0m;
        decimal lodging = 0m;

        foreach (var (code, taxAmount) in taxByCode)
        {
            var (bucket, ratePercent) = Resolve(code, catalog);
            var gross = ratePercent == 0m
                ? Round2(totalGross)
                : TaxToGross(taxAmount, ratePercent);
            if (gross == 0m)
                continue;

            switch (bucket)
            {
                case Bucket.Normal:
                    normal += gross;
                    break;
                case Bucket.Reduced1:
                    reduced1 += gross;
                    break;
                case Bucket.Reduced2:
                    reduced2 += gross;
                    break;
                case Bucket.Zero:
                    zero += gross;
                    break;
                case Bucket.Lodging:
                    lodging += gross;
                    break;
            }
        }

        return new RksvTaxSetAmounts
        {
            Normal = Round2(normal),
            Ermaessigt1 = Round2(reduced1),
            Ermaessigt2 = Round2(reduced2),
            Null = Round2(zero),
            Besonders = Round2(lodging),
        };
    }

    private enum Bucket
    {
        Normal,
        Reduced1,
        Reduced2,
        Zero,
        Lodging,
    }

    private static (Bucket Bucket, decimal RatePercent) Resolve(string code, IReadOnlyList<CountryTaxType> catalog)
    {
        var row = catalog.FirstOrDefault(type =>
            string.Equals(type.Code, code, StringComparison.OrdinalIgnoreCase));
        if (row is not null)
            return (BucketFor(row.Code), row.Rate);

        if (string.Equals(code, CountryTaxTypeCodes.Zero, StringComparison.OrdinalIgnoreCase))
            return (Bucket.Zero, 0m);

        throw new ArgumentException(
            $"Unknown CH tax_details code '{code}'. Expected a CH CountryTaxType code.");
    }

    private static Bucket BucketFor(string code)
    {
        if (string.Equals(code, CountryTaxTypeCodes.Standard, StringComparison.OrdinalIgnoreCase))
            return Bucket.Normal;
        if (string.Equals(code, CountryTaxTypeCodes.Reduced1, StringComparison.OrdinalIgnoreCase))
            return Bucket.Reduced1;
        if (string.Equals(code, CountryTaxTypeCodes.Reduced2, StringComparison.OrdinalIgnoreCase))
            return Bucket.Reduced2;
        if (string.Equals(code, CountryTaxTypeCodes.Lodging, StringComparison.OrdinalIgnoreCase))
            return Bucket.Lodging;
        if (string.Equals(code, CountryTaxTypeCodes.Zero, StringComparison.OrdinalIgnoreCase))
            return Bucket.Zero;

        throw new ArgumentException($"CH CountryTaxType '{code}' has no projection bucket.");
    }

    private static decimal TaxToGross(decimal taxAmount, decimal ratePercent)
    {
        if (taxAmount == 0m)
            return 0m;
        return Round2(taxAmount * (100m + ratePercent) / ratePercent);
    }

    private static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
