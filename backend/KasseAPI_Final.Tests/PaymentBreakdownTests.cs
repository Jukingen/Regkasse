using KasseAPI_Final.Models.Reports;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentBreakdownTests
{
    [Fact]
    public void CreateDemo_ReturnsExpectedSampleTotals()
    {
        var demo = PaymentBreakdown.CreateDemo();

        Assert.Equal(100m, demo.Cash);
        Assert.Equal(50m, demo.Card);
        Assert.Equal(25m, demo.Voucher);
        Assert.Equal(0m, demo.Other);
        Assert.Equal(175m, demo.Total);
    }

    [Fact]
    public void FromAmounts_ComputesTotal()
    {
        var breakdown = PaymentBreakdown.FromAmounts(80m, 40m, 10m, 5m);

        Assert.Equal(80m, breakdown.Cash);
        Assert.Equal(40m, breakdown.Card);
        Assert.Equal(10m, breakdown.Voucher);
        Assert.Equal(5m, breakdown.Other);
        Assert.Equal(135m, breakdown.Total);
    }
}
