using KasseAPI_Final.Tse;
using KasseAPI_Final.Tse.Fiskaly;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyReceiptSchemaMapperTests
{
    [Fact]
    public void FromTaxSets_MapsNonZeroBuckets()
    {
        var rows = FiskalyReceiptSchemaMapper.FromTaxSets(new RksvTaxSetAmounts
        {
            Normal = 10.00m,
            Ermaessigt1 = 5.50m,
            Null = 1.00m
        });

        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, r => r.VatRate == "STANDARD" && r.Amount == 10.00m);
        Assert.Contains(rows, r => r.VatRate == "REDUCED_1" && r.Amount == 5.50m);
        Assert.Contains(rows, r => r.VatRate == "ZERO" && r.Amount == 1.00m);
    }

    [Fact]
    public void FromTaxSets_ZeroTotals_UsesNullVatRate()
    {
        var rows = FiskalyReceiptSchemaMapper.FromTaxSets(RksvTaxSetAmounts.Zero);
        var row = Assert.Single(rows);
        Assert.Equal("NULL", row.VatRate);
        Assert.Equal(0m, row.Amount);
    }

    [Theory]
    [InlineData("cash", "CASH")]
    [InlineData("card", "NON_CASH")]
    [InlineData("voucher", "VOUCHER")]
    [InlineData("0", "CASH")]
    [InlineData("1", "NON_CASH")]
    [InlineData("4", "VOUCHER")]
    [InlineData(null, "CASH")]
    public void MapPaymentType_KnownValues(string? input, string expected)
    {
        Assert.Equal(expected, FiskalyReceiptSchemaMapper.MapPaymentType(input));
    }

    [Fact]
    public void MapReceiptType_Cancellation()
    {
        Assert.Equal("CANCELLATION", FiskalyReceiptSchemaMapper.MapReceiptType(true));
        Assert.Equal("NORMAL", FiskalyReceiptSchemaMapper.MapReceiptType(false));
    }
}
