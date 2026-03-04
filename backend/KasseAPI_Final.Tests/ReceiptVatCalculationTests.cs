using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Fiş VAT hesaplama ve determinizm testleri: karışık oranlar, modifier, qty, yuvarlama.
/// Aynı girdi => aynı çıktı (RKSV/TSE için).
/// </summary>
public class ReceiptVatCalculationTests
{
    /// <summary>10% ve 20% karışık sepet: toplamlar ve breakdown doğru.</summary>
    [Fact]
    public void BuildReceiptTotalsAndBreakdown_Mixed10And20Percent_CorrectTotalsAndBreakdown()
    {
        var lineSpeisen = CartMoneyHelper.ComputeLine(6.90m, 1, 10m);   // Döner 10%
        var lineGetraenke = CartMoneyHelper.ComputeLine(2.50m, 1, 20m);  // Cola 20%
        var (totals, breakdown) = CartMoneyHelper.BuildReceiptTotalsAndBreakdown([lineSpeisen, lineGetraenke]);

        Assert.Equal(9.40m, totals.TotalGross);
        Assert.True(totals.TotalNet + totals.TotalVat == totals.TotalGross);
        Assert.Equal(2, breakdown.Count);

        var b10 = breakdown.Find(b => b.VatRatePercent == 10m);
        var b20 = breakdown.Find(b => b.VatRatePercent == 20m);
        Assert.NotNull(b10);
        Assert.NotNull(b20);
        Assert.Equal(6.27m, b10.NetAmount);   // 6.90/1.1
        Assert.Equal(0.63m, b10.VatAmount);
        Assert.Equal(6.90m, b10.GrossAmount);
        Assert.Equal(2.08m, b20.NetAmount);    // 2.50/1.2
        Assert.Equal(0.42m, b20.VatAmount);
        Assert.Equal(2.50m, b20.GrossAmount);
    }

    /// <summary>Modifier dahil: ana ürün + extra (parent VAT); toplamlar satır toplamlarından.</summary>
    [Fact]
    public void BuildReceiptTotalsAndBreakdown_WithModifierSameVat_LineTotalsSumToReceiptTotals()
    {
        var lineProduct = CartMoneyHelper.ComputeLine(6.90m, 1, 10m);   // Döner 10%
        var lineModifier = CartMoneyHelper.ComputeLine(1.50m, 1, 10m);  // Extra Fleisch 10% (parent VAT)
        var (totals, breakdown) = CartMoneyHelper.BuildReceiptTotalsAndBreakdown([lineProduct, lineModifier]);

        Assert.Equal(8.40m, totals.TotalGross);
        Assert.Single(breakdown);
        Assert.Equal(10m, breakdown[0].VatRatePercent);
        Assert.Equal(7.63m, breakdown[0].NetAmount);   // 6.27 + 1.36 (line net from Round)
        Assert.Equal(0.77m, breakdown[0].VatAmount);  // 0.63 + 0.14
        Assert.Equal(8.40m, breakdown[0].GrossAmount);
        Assert.True(Math.Abs((totals.TotalNet + totals.TotalVat) - totals.TotalGross) <= 0.01m);
    }

    /// <summary>Qty > 1: satır brüt = unitGross * qty, net/vat doğru yuvarlanır.</summary>
    [Fact]
    public void BuildReceiptTotalsAndBreakdown_QtyGreaterThanOne_LineAndTotalsCorrect()
    {
        var line1 = CartMoneyHelper.ComputeLine(2.50m, 3, 20m);  // 3x Cola
        var line2 = CartMoneyHelper.ComputeLine(6.90m, 2, 10m);  // 2x Döner
        var (totals, breakdown) = CartMoneyHelper.BuildReceiptTotalsAndBreakdown([line1, line2]);

        Assert.Equal(7.50m, line1.LineGross);
        Assert.Equal(21.30m, totals.TotalGross);  // 7.50 + 13.80 (2×6.90)
        var sumNet = breakdown.Sum(b => b.NetAmount);
        var sumVat = breakdown.Sum(b => b.VatAmount);
        var sumGross = breakdown.Sum(b => b.GrossAmount);
        Assert.Equal(totals.TotalNet, sumNet);
        Assert.Equal(totals.TotalVat, sumVat);
        Assert.Equal(totals.TotalGross, sumGross);
    }

    /// <summary>Yuvarlama kenar durumu €0.99: deterministik sonuç.</summary>
    [Fact]
    public void BuildReceiptTotalsAndBreakdown_RoundingEdgeCase_099_Deterministic()
    {
        var line = CartMoneyHelper.ComputeLine(0.99m, 3, 1);  // 20% tax
        var (totals1, breakdown1) = CartMoneyHelper.BuildReceiptTotalsAndBreakdown([line]);
        var (totals2, breakdown2) = CartMoneyHelper.BuildReceiptTotalsAndBreakdown([line]);

        Assert.Equal(2.97m, totals1.TotalGross);
        Assert.Equal(2.48m, totals1.TotalNet);
        Assert.Equal(0.49m, totals1.TotalVat);
        Assert.Equal(totals1.TotalNet, totals2.TotalNet);
        Assert.Equal(totals1.TotalVat, totals2.TotalVat);
        Assert.Equal(totals1.TotalGross, totals2.TotalGross);
        Assert.Single(breakdown1);
        Assert.Equal(breakdown1[0].NetAmount, breakdown2[0].NetAmount);
        Assert.Equal(breakdown1[0].VatAmount, breakdown2[0].VatAmount);
    }

    /// <summary>Aynı girdi iki kez verildiğinde toplam ve breakdown birebir aynı (determinizm).</summary>
    [Fact]
    public void BuildReceiptTotalsAndBreakdown_SameInputTwice_IdenticalOutput()
    {
        var lines = new[]
        {
            CartMoneyHelper.ComputeLine(6.90m, 1, 10m),
            CartMoneyHelper.ComputeLine(1.50m, 1, 10m),
            CartMoneyHelper.ComputeLine(2.50m, 2, 20m)
        };
        var (t1, b1) = CartMoneyHelper.BuildReceiptTotalsAndBreakdown(lines);
        var (t2, b2) = CartMoneyHelper.BuildReceiptTotalsAndBreakdown(lines);

        Assert.Equal(t1.TotalNet, t2.TotalNet);
        Assert.Equal(t1.TotalVat, t2.TotalVat);
        Assert.Equal(t1.TotalGross, t2.TotalGross);
        Assert.Equal(b1.Count, b2.Count);
        for (var i = 0; i < b1.Count; i++)
        {
            Assert.Equal(b1[i].VatRatePercent, b2[i].VatRatePercent);
            Assert.Equal(b1[i].NetAmount, b2[i].NetAmount);
            Assert.Equal(b1[i].VatAmount, b2[i].VatAmount);
            Assert.Equal(b1[i].GrossAmount, b2[i].GrossAmount);
        }
    }

    /// <summary>VAT kesir dönüşümü: 10% → 0.10, 20% → 0.20.</summary>
    [Fact]
    public void VatPercentToFraction_10And20_CorrectFraction()
    {
        Assert.Equal(0.10m, CartMoneyHelper.VatPercentToFraction(10m));
        Assert.Equal(0.20m, CartMoneyHelper.VatPercentToFraction(20m));
    }

    /// <summary>ComputeLine(vatRatePercent) ile TaxType overload aynı sonucu üretir.</summary>
    [Fact]
    public void ComputeLine_VatPercentVsTaxType_SameResult()
    {
        var byPercent = CartMoneyHelper.ComputeLine(10.00m, 1, 20m);
        var byType = CartMoneyHelper.ComputeLine(10.00m, 1, 1); // taxType 1 = 20%
        Assert.Equal(byType.LineGross, byPercent.LineGross);
        Assert.Equal(byType.LineNet, byPercent.LineNet);
        Assert.Equal(byType.LineTax, byPercent.LineTax);
    }
}
