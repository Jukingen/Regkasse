namespace KasseAPI_Final.Services;

public enum DemoImportPriceAdjustmentMode
{
    None = 0,
    IncreasePercent = 1,
    DecreasePercent = 2,
    RoundUpToIncrement = 3,
}

/// <summary>Applies optional bulk price rules during demo product import.</summary>
internal static class DemoProductPriceAdjustment
{
    private const MidpointRounding Rounding = MidpointRounding.AwayFromZero;

    internal static decimal Apply(decimal price, DemoImportPriceAdjustmentMode mode, decimal percent, decimal roundIncrement)
    {
        if (mode == DemoImportPriceAdjustmentMode.None || price < 0)
            return RoundPrice(price);

        return mode switch
        {
            DemoImportPriceAdjustmentMode.IncreasePercent => ApplyPercent(price, percent, increase: true),
            DemoImportPriceAdjustmentMode.DecreasePercent => ApplyPercent(price, percent, increase: false),
            DemoImportPriceAdjustmentMode.RoundUpToIncrement => RoundUpToIncrement(price, roundIncrement),
            _ => RoundPrice(price),
        };
    }

    internal static DemoImportPriceAdjustmentMode ParseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DemoImportPriceAdjustmentMode.None;

        if (Enum.TryParse<DemoImportPriceAdjustmentMode>(value, ignoreCase: true, out var parsed))
            return parsed;

        return value.Trim().ToLowerInvariant() switch
        {
            "increase" or "increasepercent" => DemoImportPriceAdjustmentMode.IncreasePercent,
            "decrease" or "decreasepercent" => DemoImportPriceAdjustmentMode.DecreasePercent,
            "roundup" or "rounduptoincrement" => DemoImportPriceAdjustmentMode.RoundUpToIncrement,
            _ => DemoImportPriceAdjustmentMode.None,
        };
    }

    private static decimal ApplyPercent(decimal price, decimal percent, bool increase)
    {
        var safePercent = Math.Clamp(percent, 0m, 1000m);
        var factor = safePercent / 100m;
        var adjusted = increase ? price * (1m + factor) : price * (1m - factor);
        return RoundPrice(Math.Max(0m, adjusted));
    }

    private static decimal RoundUpToIncrement(decimal price, decimal increment)
    {
        if (increment <= 0m)
            return RoundPrice(price);

        var steps = Math.Ceiling(price / increment);
        return RoundPrice(steps * increment);
    }

    private static decimal RoundPrice(decimal value) =>
        Math.Round(value, 2, Rounding);
}
