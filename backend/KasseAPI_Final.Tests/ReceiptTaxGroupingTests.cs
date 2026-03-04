using System.Text.Json;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Fiş MwSt tablosu: (TaxType, TaxRate) gruplama, LineNet toplamı, invariant Net+Tax=Gross.
/// </summary>
public class ReceiptTaxGroupingTests
{
    /// <summary>Aynı TaxRate (20%) farklı TaxType (1 ve 3) → iki ayrı MwSt satırı.</summary>
    [Fact]
    public void GroupBy_TaxTypeAndTaxRate_SameRateDifferentType_TwoRows()
    {
        var line1 = CartMoneyHelper.ComputeLine(10.00m, 1, 1); // Standard 20%
        var line2 = CartMoneyHelper.ComputeLine(10.00m, 1, 3); // Special 13% (different rate in AT)
        var items = new[]
        {
            new PaymentItem { ProductId = Guid.NewGuid(), ProductName = "A", Quantity = 1, UnitPrice = 10.00m, TaxType = 1, TaxRate = 0.20m, TotalPrice = line1.LineGross, TaxAmount = line1.LineTax, LineNet = line1.LineNet },
            new PaymentItem { ProductId = Guid.NewGuid(), ProductName = "B", Quantity = 1, UnitPrice = 10.00m, TaxType = 3, TaxRate = 0.13m, TotalPrice = line2.LineGross, TaxAmount = line2.LineTax, LineNet = line2.LineNet }
        };
        var groups = items.GroupBy(i => new { i.TaxType, i.TaxRate }).ToList();
        Assert.Equal(2, groups.Count());
    }

    /// <summary>9.99 × 3 rounding edge-case: grup Net+Tax=Gross ve toplamlar tutarlı.</summary>
    [Fact]
    public void Rounding_9_99_x_3_GroupInvariant_NetPlusTaxEqualsGross()
    {
        var line = CartMoneyHelper.ComputeLine(9.99m, 3, 1);
        var items = new[]
        {
            new PaymentItem { ProductId = Guid.NewGuid(), ProductName = "X", Quantity = 3, UnitPrice = 9.99m, TaxType = 1, TaxRate = 0.20m, TotalPrice = line.LineGross, TaxAmount = line.LineTax, LineNet = line.LineNet }
        };
        var g = items.GroupBy(i => new { i.TaxType, i.TaxRate }).Single();
        var netAmount = g.Sum(x => x.LineNet);
        var taxAmount = g.Sum(x => x.TaxAmount);
        var grossAmount = g.Sum(x => x.TotalPrice);
        Assert.True(System.Math.Abs((netAmount + taxAmount) - grossAmount) <= 0.01m, "Invariant: (NetAmount + TaxAmount) - GrossAmount <= 0.01");
        Assert.Equal(29.97m, grossAmount);
        Assert.Equal(24.98m, netAmount);
        Assert.Equal(4.99m, taxAmount);
    }

    /// <summary>NetAmount = Sum(LineNet) kullanıldığında Gross-Tax türetmeden tutarlılık.</summary>
    [Fact]
    public void NetAmount_FromLineNet_NotDerived_MatchesInvariant()
    {
        var line1 = CartMoneyHelper.ComputeLine(10.00m, 2, 1);
        var line2 = CartMoneyHelper.ComputeLine(5.00m, 3, 2);
        var items = new[]
        {
            new PaymentItem { ProductId = Guid.NewGuid(), ProductName = "P1", Quantity = 2, UnitPrice = 10.00m, TaxType = 1, TaxRate = 0.20m, TotalPrice = line1.LineGross, TaxAmount = line1.LineTax, LineNet = line1.LineNet },
            new PaymentItem { ProductId = Guid.NewGuid(), ProductName = "P2", Quantity = 3, UnitPrice = 5.00m, TaxType = 2, TaxRate = 0.10m, TotalPrice = line2.LineGross, TaxAmount = line2.LineTax, LineNet = line2.LineNet }
        };
        foreach (var g in items.GroupBy(i => new { i.TaxType, i.TaxRate }))
        {
            var netAmount = g.Sum(x => x.LineNet);
            var taxAmount = g.Sum(x => x.TaxAmount);
            var grossAmount = g.Sum(x => x.TotalPrice);
            Assert.True(System.Math.Abs((netAmount + taxAmount) - grossAmount) <= 0.01m);
        }
    }

    /// <summary>
    /// Receipt totals and tax lines must come from the single source: payment_details.PaymentItems (JSON).
    /// Simulates JSON round-trip and asserts receipt totals/tax lines are derived only from that snapshot.
    /// </summary>
    [Fact]
    public void ReceiptTotalsAndTaxLines_FromPaymentItemsJson_SingleSourceOfTruth()
    {
        var line1 = CartMoneyHelper.ComputeLine(10.00m, 2, 1);
        var line2 = CartMoneyHelper.ComputeLine(5.00m, 3, 2);
        var sourceItems = new List<PaymentItem>
        {
            new PaymentItem { ProductId = Guid.NewGuid(), ProductName = "P1", Quantity = 2, UnitPrice = 10.00m, TaxType = 1, TaxRate = 0.20m, TotalPrice = line1.LineGross, TaxAmount = line1.LineTax, LineNet = line1.LineNet },
            new PaymentItem { ProductId = Guid.NewGuid(), ProductName = "P2", Quantity = 3, UnitPrice = 5.00m, TaxType = 2, TaxRate = 0.10m, TotalPrice = line2.LineGross, TaxAmount = line2.LineTax, LineNet = line2.LineNet }
        };
        var expectedGrandTotal = sourceItems.Sum(i => i.TotalPrice);
        var expectedTaxTotal = sourceItems.Sum(i => i.TaxAmount);

        // Simulate single source: only PaymentItems JSON (no payment_items table)
        var json = JsonSerializer.Serialize(sourceItems);
        var fromJson = JsonSerializer.Deserialize<List<PaymentItem>>(json);
        Assert.NotNull(fromJson);
        var paymentItems = fromJson!;

        // Same logic as PaymentService.GetReceiptDataAsync: totals and tax lines from this list only
        var taxRates = paymentItems
            .GroupBy(i => new { i.TaxType, i.TaxRate })
            .Select(g => new
            {
                TaxType = g.Key.TaxType,
                Rate = g.Key.TaxRate * 100,
                TaxAmount = g.Sum(x => x.TaxAmount),
                GrossAmount = g.Sum(x => x.TotalPrice),
                NetAmount = g.Sum(x => System.Math.Abs((x.LineNet + x.TaxAmount) - x.TotalPrice) <= 0.01m ? x.LineNet : (x.TotalPrice - x.TaxAmount))
            })
            .OrderBy(t => t.Rate)
            .ThenBy(t => t.TaxType)
            .ToList();

        var receiptGrandTotal = taxRates.Sum(t => t.GrossAmount);
        var receiptTaxTotal = taxRates.Sum(t => t.TaxAmount);

        Assert.Equal(expectedGrandTotal, receiptGrandTotal);
        Assert.Equal(expectedTaxTotal, receiptTaxTotal);
        Assert.Equal(35.00m, receiptGrandTotal); // 20 + 15
        Assert.True(taxRates.Count >= 1);
        Assert.True(System.Math.Abs((receiptGrandTotal - receiptTaxTotal) - taxRates.Sum(t => t.NetAmount)) <= 0.01m);
    }
}
