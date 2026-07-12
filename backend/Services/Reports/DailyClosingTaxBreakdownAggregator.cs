using System.Globalization;
using System.Text.Json;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services.Reports;

/// <summary>Aggregates invoice/payment tax JSON into RKSV report VAT buckets.</summary>
public static class DailyClosingTaxBreakdownAggregator
{
    public static DailyClosingTaxBreakdownDto AggregateFromTaxDetailsJsonDocuments(
        IEnumerable<JsonDocument> taxDocuments)
    {
        var taxByType = new Dictionary<int, decimal>();
        foreach (var doc in taxDocuments)
        {
            MergeTaxDocument(doc, taxByType);
        }

        return MapToBreakdown(taxByType);
    }

    private static void MergeTaxDocument(JsonDocument taxDetails, Dictionary<int, decimal> taxByType)
    {
        try
        {
            if (taxDetails.RootElement.ValueKind != JsonValueKind.Object)
                return;

            foreach (var prop in taxDetails.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Number || !prop.Value.TryGetDecimal(out var amount))
                    continue;

                var taxType = ResolveTaxTypeKey(prop.Name);
                if (!taxType.HasValue)
                    continue;

                taxByType[taxType.Value] = taxByType.GetValueOrDefault(taxType.Value) + amount;
            }
        }
        catch
        {
            // ignore malformed tax JSON
        }
    }

    private static int? ResolveTaxTypeKey(string key)
    {
        if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)
            && TaxTypes.IsValidTaxType(numeric))
            return numeric;

        return key.Trim().ToLowerInvariant() switch
        {
            "standard" => TaxTypes.Standard,
            "reduced" => TaxTypes.Reduced,
            "special" => TaxTypes.Special,
            "zerorate" => TaxTypes.ZeroRate,
            _ => null,
        };
    }

    private static DailyClosingTaxBreakdownDto MapToBreakdown(Dictionary<int, decimal> taxByType)
    {
        decimal Gross(int taxType, decimal taxAmount)
        {
            if (taxAmount == 0m)
                return 0m;
            var rate = TaxTypes.GetTaxRate(taxType);
            if (rate <= 0m)
                return Round2(taxAmount);
            return Round2(taxAmount * (100m + rate) / rate);
        }

        var tax20 = taxByType.GetValueOrDefault(TaxTypes.Standard);
        var tax10 = taxByType.GetValueOrDefault(TaxTypes.Reduced);
        var tax13 = taxByType.GetValueOrDefault(TaxTypes.Special);
        var tax0 = taxByType.GetValueOrDefault(TaxTypes.ZeroRate);

        return new DailyClosingTaxBreakdownDto
        {
            TaxAt20 = Round2(tax20),
            GrossAt20 = Gross(TaxTypes.Standard, tax20),
            TaxAt10 = Round2(tax10),
            GrossAt10 = Gross(TaxTypes.Reduced, tax10),
            TaxAt13 = Round2(tax13),
            GrossAt13 = Gross(TaxTypes.Special, tax13),
            GrossAt0 = tax0 == 0m ? 0m : Round2(tax0),
        };
    }

    private static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
