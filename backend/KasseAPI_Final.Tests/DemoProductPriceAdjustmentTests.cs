using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DemoProductPriceAdjustmentTests
{
    [Theory]
    [InlineData(9.80, 10, 10.78)]
    [InlineData(9.00, 5, 9.45)]
    public void Apply_IncreasePercent_RoundsToTwoDecimals(decimal price, decimal percent, decimal expected)
    {
        var result = DemoProductPriceAdjustment.Apply(
            price,
            DemoImportPriceAdjustmentMode.IncreasePercent,
            percent,
            0.50m);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(9.80, 10, 8.82)]
    [InlineData(5.40, 5, 5.13)]
    public void Apply_DecreasePercent_RoundsToTwoDecimals(decimal price, decimal percent, decimal expected)
    {
        var result = DemoProductPriceAdjustment.Apply(
            price,
            DemoImportPriceAdjustmentMode.DecreasePercent,
            percent,
            0.50m);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(9.80, 0.50, 10.00)]
    [InlineData(9.51, 0.50, 10.00)]
    [InlineData(9.50, 0.50, 9.50)]
    [InlineData(3.05, 0.50, 3.50)]
    public void Apply_RoundUpToIncrement_CeilToNearestStep(decimal price, decimal increment, decimal expected)
    {
        var result = DemoProductPriceAdjustment.Apply(
            price,
            DemoImportPriceAdjustmentMode.RoundUpToIncrement,
            0m,
            increment);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Apply_None_ReturnsOriginalRounded()
    {
        Assert.Equal(9.80m, DemoProductPriceAdjustment.Apply(9.80m, DemoImportPriceAdjustmentMode.None, 10m, 0.50m));
    }

    [Theory]
    [InlineData("IncreasePercent", DemoImportPriceAdjustmentMode.IncreasePercent)]
    [InlineData("roundUpToIncrement", DemoImportPriceAdjustmentMode.RoundUpToIncrement)]
    public void ParseMode_AcceptsStringValues(string value, DemoImportPriceAdjustmentMode expected)
    {
        Assert.Equal(expected, DemoProductPriceAdjustment.ParseMode(value));
    }
}
