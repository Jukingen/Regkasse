using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class MonatsbelegSalesGateEvaluatorTests
{
    [Fact]
    public void Missing_Strict_AlwaysBlocks_WithRedWarning()
    {
        var d = MonatsbelegSalesGateEvaluator.Evaluate(
            sessionGateApplies: true,
            previousMonthMissing: true,
            MonatsbelegBlockingMode.Strict,
            viennaDayOfMonth: 3);

        Assert.True(d.BlocksSales);
        Assert.Equal(MonatsbelegSalesGateEvaluator.WarningRed, d.WarningLevel);
        Assert.False(d.CanContinueWithWarning);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(14)]
    public void Missing_GracePeriod_AllowsThroughDay14(int day)
    {
        var d = MonatsbelegSalesGateEvaluator.Evaluate(
            true, true, MonatsbelegBlockingMode.GracePeriod, day);

        Assert.False(d.BlocksSales);
        Assert.True(d.CanContinueWithWarning);
        Assert.Equal(
            day <= 7 ? MonatsbelegSalesGateEvaluator.WarningRed : MonatsbelegSalesGateEvaluator.WarningYellow,
            d.WarningLevel);
    }

    [Fact]
    public void Missing_GracePeriod_BlocksAfterDay14()
    {
        var d = MonatsbelegSalesGateEvaluator.Evaluate(
            true, true, MonatsbelegBlockingMode.GracePeriod, 15);

        Assert.True(d.BlocksSales);
        Assert.Equal(MonatsbelegSalesGateEvaluator.WarningRed, d.WarningLevel);
        Assert.False(d.CanContinueWithWarning);
    }

    [Fact]
    public void Missing_WarningOnly_NeverBlocks()
    {
        var d = MonatsbelegSalesGateEvaluator.Evaluate(
            true, true, MonatsbelegBlockingMode.WarningOnly, 20);

        Assert.False(d.BlocksSales);
        Assert.True(d.CanContinueWithWarning);
        Assert.Equal(MonatsbelegSalesGateEvaluator.WarningRed, d.WarningLevel);
    }

    [Fact]
    public void Present_NeverBlocks()
    {
        var d = MonatsbelegSalesGateEvaluator.Evaluate(
            true, false, MonatsbelegBlockingMode.Strict, 2);

        Assert.False(d.BlocksSales);
        Assert.Equal(MonatsbelegSalesGateEvaluator.WarningNone, d.WarningLevel);
        Assert.False(d.CanContinueWithWarning);
    }

    [Fact]
    public void SessionGateOff_NeverBlocksEvenIfMissing()
    {
        var d = MonatsbelegSalesGateEvaluator.Evaluate(
            false, true, MonatsbelegBlockingMode.Strict, 2);

        Assert.False(d.BlocksSales);
        Assert.Equal(MonatsbelegSalesGateEvaluator.WarningNone, d.WarningLevel);
    }
}
