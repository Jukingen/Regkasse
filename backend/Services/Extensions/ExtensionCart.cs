namespace KasseAPI_Final.Services.Extensions;

/// <summary>One gross cart line passed through <c>payment.before-sign</c>.</summary>
public sealed record ExtensionCartLine(
    Guid ProductId,
    int Quantity,
    int TaxType,
    decimal GrossTotal,
    bool RequiresPrescription = false);

public static class ExtensionCart
{
    public const string LinesKey = "lines";
    public const string TotalGrossKey = "totalGross";
    public const string AppliedCampaignIdsKey = "appliedCampaignIds";
    public const string HasPrescriptionKey = "hasPrescription";
    public const string PrescriptionReferenceKey = "prescriptionReference";

    public static List<ExtensionCartLine> ReadLines(IReadOnlyDictionary<string, object?>? payload)
    {
        if (payload is null || !payload.TryGetValue(LinesKey, out var raw) || raw is null)
            return [];

        if (raw is IEnumerable<ExtensionCartLine> typed)
            return typed.ToList();

        return [];
    }

    public static Dictionary<string, object?> Write(
        IReadOnlyList<ExtensionCartLine> lines,
        IReadOnlyList<Guid> appliedCampaignIds) =>
        new()
        {
            [LinesKey] = lines,
            [TotalGrossKey] = lines.Sum(line => line.GrossTotal),
            [AppliedCampaignIdsKey] = appliedCampaignIds,
        };

    public static decimal DiscountGross(decimal grossTotal, decimal discountPercent)
    {
        if (discountPercent <= 0)
            return decimal.Round(grossTotal, 2, MidpointRounding.AwayFromZero);
        if (discountPercent >= 100)
            return 0m;

        var factor = (100m - discountPercent) / 100m;
        var discounted = decimal.Round(grossTotal * factor, 2, MidpointRounding.AwayFromZero);
        return discounted < 0 ? 0 : discounted;
    }
}
