using System.Text.Json;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Payment lines use product.TaxType (same as cart); invoice PDF reads PaymentItems JSON via PaymentItem deserialization.
/// </summary>
public class PaymentInvoiceTaxPropagationTests
{
    [Fact]
    public void ComputeLine_ReducedTaxType_AlignsWithTenPercentVatPercentOverload()
    {
        var byTaxType = CartMoneyHelper.ComputeLine(21.84m, 1, TaxTypes.Reduced);
        var byPercent = CartMoneyHelper.ComputeLine(21.84m, 1, 10m);
        Assert.Equal(byPercent.LineTax, byTaxType.LineTax);
        Assert.Equal(byPercent.LineNet, byTaxType.LineNet);
        Assert.Equal(byPercent.LineGross, byTaxType.LineGross);
        Assert.Equal(TaxTypes.Reduced, byTaxType.TaxType);
        Assert.Equal(0.10m, byTaxType.TaxRate);
    }

    [Fact]
    public void ComputeLine_StandardTaxType_KeepsTwentyPercentFromTaxTypes()
    {
        var line = CartMoneyHelper.ComputeLine(100m, 1, TaxTypes.Standard);
        Assert.Equal(TaxTypes.Standard, line.TaxType);
        Assert.Equal(0.20m, line.TaxRate);
        Assert.Equal(100m, line.LineGross);
    }

    [Fact]
    public void PaymentItemsJson_RoundTrip_MatchesPdfRowFields()
    {
        var line = CartMoneyHelper.ComputeLine(21.84m, 1, TaxTypes.Reduced);
        var items = new List<PaymentItem>
        {
            new()
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Demo Product 1",
                Quantity = 1,
                UnitPrice = line.UnitPriceGross,
                TotalPrice = line.LineGross,
                TaxType = line.TaxType,
                TaxRate = line.TaxRate,
                TaxAmount = line.LineTax,
                LineNet = line.LineNet
            }
        };

        var json = JsonSerializer.Serialize(items);
        var roundTrip = JsonSerializer.Deserialize<List<PaymentItem>>(json);
        Assert.NotNull(roundTrip);
        var pi = Assert.Single(roundTrip!);
        Assert.Equal("Demo Product 1", pi.ProductName);
        Assert.Equal(1, pi.Quantity);
        Assert.Equal(21.84m, pi.UnitPrice);
        Assert.Equal(TaxTypes.Reduced, pi.TaxType);
        Assert.Equal(0.10m, pi.TaxRate);
        Assert.Equal(21.84m, pi.TotalPrice);
        Assert.Equal(line.LineTax, pi.TaxAmount);
    }
}
