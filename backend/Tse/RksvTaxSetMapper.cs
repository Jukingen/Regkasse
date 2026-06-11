using System.Globalization;
using System.Text.Json;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Tse;

/// <summary>
/// Maps persisted payment tax details to RKSV Betrag-Satz-* gross buckets.
/// </summary>
public static class RksvTaxSetMapper
{
    public static RksvTaxSetAmounts MapFromTaxDetailsJson(string? taxDetailsJson, decimal totalAmount)
    {
        var amounts = RksvTaxSetAmounts.Zero;
        if (string.IsNullOrWhiteSpace(taxDetailsJson) || taxDetailsJson == "{}")
        {
            if (totalAmount != 0m)
                return new RksvTaxSetAmounts { Normal = Round2(totalAmount) };
            return amounts;
        }

        try
        {
            using var doc = JsonDocument.Parse(taxDetailsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return Fallback(totalAmount);

            var buckets = new Dictionary<int, decimal>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!int.TryParse(prop.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var taxType))
                    continue;
                if (prop.Value.ValueKind != JsonValueKind.Number)
                    continue;
                buckets[taxType] = prop.Value.GetDecimal();
            }

            if (buckets.Count == 0)
                return Fallback(totalAmount);

            decimal normal = 0, erm1 = 0, erm2 = 0, zero = 0, besonders = 0;
            foreach (var (taxType, taxAmount) in buckets)
            {
                var gross = TaxAmountToGross(taxType, taxAmount);
                switch (taxType)
                {
                    case TaxTypes.Standard:
                        normal += gross;
                        break;
                    case TaxTypes.Reduced:
                        erm1 += gross;
                        break;
                    case TaxTypes.Special:
                        erm2 += gross;
                        break;
                    case TaxTypes.ZeroRate:
                        zero += gross;
                        break;
                    default:
                        besonders += gross;
                        break;
                }
            }

            return new RksvTaxSetAmounts
            {
                Normal = Round2(normal),
                Ermaessigt1 = Round2(erm1),
                Ermaessigt2 = Round2(erm2),
                Null = Round2(zero),
                Besonders = Round2(besonders),
            };
        }
        catch (JsonException)
        {
            return Fallback(totalAmount);
        }
    }

    private static RksvTaxSetAmounts Fallback(decimal totalAmount) =>
        totalAmount == 0m
            ? RksvTaxSetAmounts.Zero
            : new RksvTaxSetAmounts { Normal = Round2(totalAmount) };

    private static decimal TaxAmountToGross(int taxType, decimal taxAmount)
    {
        if (taxAmount == 0m)
            return 0m;
        var rate = TaxTypes.GetTaxRate(taxType);
        if (rate <= 0m)
            return Round2(taxAmount);
        return Round2(taxAmount * (100m + rate) / rate);
    }

    private static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
