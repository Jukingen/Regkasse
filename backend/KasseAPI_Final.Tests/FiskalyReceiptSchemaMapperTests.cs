using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Tse;
using KasseAPI_Final.Tse.Fiskaly;
using KasseAPI_Final.DTOs;
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
        Assert.Equal("ZERO", row.VatRate);
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
    public void BuildTagesabschlussTransaction_IsZeroAmountNormalMarker()
    {
        var registerId = Guid.NewGuid();
        var closingDate = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc);
        var data = FiskalyReceiptSchemaMapper.BuildTagesabschlussTransaction(
            registerId,
            closingDate,
            1234.50m,
            12);

        Assert.Equal("NORMAL", data.ReceiptType);
        Assert.Equal(0m, data.TotalAmount);
        Assert.Equal("ZERO", data.VatRate);
        Assert.Equal(registerId.ToString("D"), data.CashRegisterId);
        var vat = Assert.Single(data.AmountsPerVatRate!);
        Assert.Equal("ZERO", vat.VatRate);
        Assert.Equal(0m, vat.Amount);
        var line = Assert.Single(data.LineItems!);
        Assert.Equal("0.00", line.PricePerUnit);
        Assert.Contains("Tagesabschluss 2026-08-29", line.Text, StringComparison.Ordinal);
        Assert.Contains("1234.50", line.Text, StringComparison.Ordinal);
        Assert.Contains("12", line.Text, StringComparison.Ordinal);

        var request = FiskalyReceiptSchemaMapper.BuildSignRequest(data);
        Assert.Equal("NORMAL", request.ReceiptType);
        var payment = Assert.Single(request.Schema.StandardV1!.AmountsPerPaymentType);
        Assert.Equal("0.00", payment.Amount);
    }

    [Fact]
    public void MapReceiptType_Cancellation()
    {
        Assert.Equal("CANCELLATION", FiskalyReceiptSchemaMapper.MapReceiptType(true));
        Assert.Equal("NORMAL", FiskalyReceiptSchemaMapper.MapReceiptType(false));
    }

    [Fact]
    public void BuildSignRequest_Cancellation_UsesNegativeAmounts()
    {
        var request = FiskalyReceiptSchemaMapper.BuildSignRequest(new FiskalyTransactionData
        {
            ReceiptType = FiskalyReceiptSchemaMapper.ReceiptTypeCancellation,
            PaymentType = "CASH",
            AmountsPerVatRate =
            [
                new FiskalyVatAmount { VatRate = "STANDARD", Amount = -10.00m }
            ]
        });

        Assert.Equal("CANCELLATION", request.ReceiptType);
        var vat = Assert.Single(request.Schema.StandardV1!.AmountsPerVatRate);
        Assert.Equal("STANDARD", vat.VatRate);
        Assert.Equal("-10.00", vat.Amount);
        var payment = Assert.Single(request.Schema.StandardV1.AmountsPerPaymentType);
        Assert.Equal("-10.00", payment.Amount);
        var line = Assert.Single(request.Schema.StandardV1.LineItems);
        Assert.Equal("-10.00", line.PricePerUnit);
    }

    [Fact]
    public void BuildSignRequest_StandardV1_OmitsNestedReceiptAndIncludesLineItems()
    {
        var request = FiskalyReceiptSchemaMapper.BuildSignRequest(new FiskalyTransactionData
        {
            ReceiptType = "NORMAL",
            PaymentType = "CASH",
            AmountsPerVatRate =
            [
                new FiskalyVatAmount { VatRate = "STANDARD", Amount = 10.00m }
            ],
            LineItems =
            [
                new FiskalyLineItem
                {
                    Quantity = "1",
                    Text = "Test Produkt",
                    PricePerUnit = "10.00"
                }
            ]
        });

        Assert.Equal("NORMAL", request.ReceiptType);
        Assert.NotNull(request.Schema.StandardV1);
        Assert.Null(request.Schema.Raw);
        var standard = request.Schema.StandardV1;

        var vat = Assert.Single(standard.AmountsPerVatRate);
        Assert.Equal("STANDARD", vat.VatRate);
        Assert.Equal("10.00", vat.Amount);

        var line = Assert.Single(standard.LineItems);
        Assert.Equal("1", line.Quantity);
        Assert.Equal("Test Produkt", line.Text);
        Assert.Equal("10.00", line.PricePerUnit);

        var payment = Assert.Single(standard.AmountsPerPaymentType);
        Assert.Equal("CASH", payment.PaymentType);
        Assert.Equal("10.00", payment.Amount);
        Assert.Equal("EUR", payment.CurrencyCode);
    }

    [Fact]
    public void BuildSignRequest_Raw_MapsGrossAmounts()
    {
        var request = FiskalyReceiptSchemaMapper.BuildSignRequest(new FiskalyTransactionData
        {
            SchemaKind = FiskalyReceiptSchemaKinds.Raw,
            AmountsPerVatRate =
            [
                new FiskalyVatAmount { VatRate = "STANDARD", Amount = 10.00m },
                new FiskalyVatAmount { VatRate = "REDUCED_1", Amount = 5.50m }
            ]
        });

        Assert.Null(request.Schema.StandardV1);
        Assert.NotNull(request.Schema.Raw);
        var raw = request.Schema.Raw;
        Assert.Equal("10.00", raw.GrossAmountStandard);
        Assert.Equal("5.50", raw.GrossAmountReduced1);
        Assert.Equal("0.00", raw.GrossAmountReduced2);
        Assert.Equal("0.00", raw.GrossAmountSpecial);
        Assert.Equal("0.00", raw.GrossAmountZero);
    }

    [Fact]
    public void BuildSignRequest_SynthesizesLineItemWhenMissing()
    {
        var request = FiskalyReceiptSchemaMapper.BuildSignRequest(new FiskalyTransactionData
        {
            TotalAmount = 12.00m,
            VatRate = "STANDARD"
        });

        var line = Assert.Single(request.Schema.StandardV1!.LineItems);
        Assert.Equal("Verkauf", line.Text);
        Assert.Equal("12.00", line.PricePerUnit);
    }

    [Fact]
    public void BuildSignRequest_StandardV1_SerializesWithoutNestedReceipt()
    {
        var request = FiskalyReceiptSchemaMapper.BuildSignRequest(new FiskalyTransactionData
        {
            AmountsPerVatRate = [new FiskalyVatAmount { VatRate = "STANDARD", Amount = 10.00m }],
            LineItems =
            [
                new FiskalyLineItem { Quantity = "1", Text = "Test Produkt", PricePerUnit = "10.00" }
            ]
        });

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        Assert.Contains("\"standard_v1\":{", json, StringComparison.Ordinal);
        Assert.Contains("\"amounts_per_vat_rate\":", json, StringComparison.Ordinal);
        Assert.Contains("\"line_items\":", json, StringComparison.Ordinal);
        Assert.Contains("\"Test Produkt\"", json, StringComparison.Ordinal);
        Assert.Contains("\"currency_code\":\"EUR\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"receipt\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"raw\":", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SignTestScenario_Raw_UsesRawSchemaKind()
    {
        var scenario = FiskalySignTestScenarios.Find(FiskalySignTestScenarioIds.Raw);
        Assert.NotNull(scenario);
        var data = FiskalySignTestScenarios.ToTransactionData(scenario!, Guid.NewGuid());
        Assert.Equal(FiskalyReceiptSchemaKinds.Raw, data.SchemaKind);
        Assert.Equal("Test Produkt", Assert.Single(data.LineItems!).Text);

        var request = FiskalyReceiptSchemaMapper.BuildSignRequest(data);
        Assert.NotNull(request.Schema.Raw);
        Assert.Equal("10.00", request.Schema.Raw!.GrossAmountStandard);
        Assert.Null(request.Schema.StandardV1);
    }
}
