using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Cart vergi hesaplama testleri - gross model (Product.Price = inkl. MwSt.)
/// </summary>
public class CartMoneyHelperTests
{
    [Fact]
    public void ComputeLine_EmbeddedTax_10Gross_20Percent_Returns167()
    {
        var line = CartMoneyHelper.ComputeLine(10.00m, 1, 1);
        Assert.Equal(1.67m, line.LineTax);
    }

    [Fact]
    public void ComputeLine_OneItem10Gross_20Percent_CorrectAmounts()
    {
        var line = CartMoneyHelper.ComputeLine(10.00m, 1, 1); // taxType 1 = Standard 20%
        Assert.Equal(10.00m, line.UnitPriceGross);
        Assert.Equal(10.00m, line.LineGross);
        Assert.Equal(1.67m, line.LineTax);
        Assert.Equal(8.33m, line.LineNet);
    }

    [Fact]
    public void ComputeCartTotals_SingleItem10Gross_GrandTotal10()
    {
        var line = CartMoneyHelper.ComputeLine(10.00m, 1, 1);
        var totals = CartMoneyHelper.ComputeCartTotals([line]);
        Assert.Equal(10.00m, totals.GrandTotalGross);
        Assert.Equal(1.67m, totals.IncludedTaxTotal);
        Assert.Equal(8.33m, totals.SubtotalNet);
    }

    [Fact]
    public void ComputeCartTotals_MultipleTaxTypes_CorrectTaxSummary()
    {
        var line20 = CartMoneyHelper.ComputeLine(10.00m, 1, 1); // 20%
        var line10 = CartMoneyHelper.ComputeLine(10.00m, 1, 2); // 10%
        var totals = CartMoneyHelper.ComputeCartTotals([line20, line10]);

        Assert.Equal(20.00m, totals.GrandTotalGross);
        Assert.Equal(2, totals.TaxSummary.Count);

        var s20 = totals.TaxSummary.Find(t => t.TaxType == 1);
        Assert.NotNull(s20);
        Assert.Equal(20m, s20.TaxRatePct);
        Assert.Equal(10.00m, s20.GrossAmount);
        Assert.Equal(1.67m, s20.TaxAmount);

        var s10 = totals.TaxSummary.Find(t => t.TaxType == 2);
        Assert.NotNull(s10);
        Assert.Equal(10m, s10.TaxRatePct);
        Assert.Equal(10.00m, s10.GrossAmount);
        Assert.Equal(0.91m, s10.TaxAmount); // 10/1.1 - 10 ≈ 0.91
    }

    [Fact]
    public void ComputeLine_Rounding_EdgeCase()
    {
        var line = CartMoneyHelper.ComputeLine(0.99m, 3, 1);
        Assert.Equal(2.97m, line.LineGross);
        Assert.Equal(2.48m, line.LineNet);
        Assert.Equal(0.49m, line.LineTax);
    }

    [Fact]
    public void ComputeLine_ZeroRate_NetEqualsGross()
    {
        var line = CartMoneyHelper.ComputeLine(10.00m, 1, 4); // taxType 4 = ZeroRate
        Assert.Equal(10.00m, line.LineGross);
        Assert.Equal(10.00m, line.LineNet);
        Assert.Equal(0.00m, line.LineTax);
    }

    [Fact]
    public void ComputeLine_Qty3_Unit10_20Percent_ExactIntegers()
    {
        var line = CartMoneyHelper.ComputeLine(10.00m, 3, 1);
        Assert.Equal(30.00m, line.LineGross);
        Assert.Equal(25.00m, line.LineNet);
        Assert.Equal(5.00m, line.LineTax);
    }

    [Fact]
    public void ComputeLine_Gross999_Qty3_RoundingEdge()
    {
        var line = CartMoneyHelper.ComputeLine(9.99m, 3, 1);
        Assert.Equal(29.97m, line.LineGross);
        Assert.Equal(24.98m, line.LineNet);
        Assert.Equal(4.99m, line.LineTax);
    }

    [Fact]
    public void ComputeCartTotals_MixedTaxTypes_LineTotalsMatchHeader()
    {
        var line20a = CartMoneyHelper.ComputeLine(10.00m, 2, 1); // 20%
        var line10a = CartMoneyHelper.ComputeLine(5.00m, 3, 2);   // 10%
        var totals = CartMoneyHelper.ComputeCartTotals([line20a, line10a]);

        var sumNet = totals.TaxSummary.Sum(t => t.NetAmount);
        var sumTax = totals.TaxSummary.Sum(t => t.TaxAmount);
        var sumGross = totals.TaxSummary.Sum(t => t.GrossAmount);

        Assert.Equal(totals.SubtotalNet, sumNet);
        Assert.Equal(totals.IncludedTaxTotal, sumTax);
        Assert.Equal(totals.GrandTotalGross, sumGross);
        Assert.True(Math.Abs((totals.SubtotalNet + totals.IncludedTaxTotal) - totals.SubtotalGross) <= 0.01m);
    }

    [Fact]
    public void ComputeGrossFromNet_MatchesGrossModelSemantics()
    {
        var (lineTax, lineGross) = CartMoneyHelper.ComputeGrossFromNet(8.33m, 0.20m);
        Assert.Equal(1.67m, lineTax);
        Assert.Equal(10.00m, lineGross);
    }
}
