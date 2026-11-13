using System.Text;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Order;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LieferandoOrderMapperTests
{
    [Fact]
    public void Maps_plu_children_and_minor_units_onto_the_normalized_body()
    {
        var mapped = LieferandoOrderMapper.Map(Sample());

        Assert.True(mapped.Parsed);
        Assert.Null(mapped.Error);
        Assert.Equal("38bbeb45-f520-4438-a44f-0fcdbb29e166", mapped.OrderId);
        Assert.Equal("AKZ12", mapped.LocationId);
        var body = JsonSerializer.Deserialize<OnlineOrderIntegrationWebhookBody>(mapped.NormalizedJson!, JsonOptions())!;
        Assert.Equal("38bbeb45-f520-4438-a44f-0fcdbb29e166", body.ExternalOrderId);
        Assert.Equal("AKZ12", body.ExternalLocationId);
        Assert.Null(body.ExternalChannel);
        Assert.Equal("delivery", body.OrderType);
        Assert.Equal(18.00m, body.ChannelTotal);
        Assert.Equal(0m, body.DeliveryFee);
        Assert.Equal("Anna Berger", body.CustomerName);
        Assert.Equal("+431234567", body.CustomerPhone);
        Assert.Contains("22721763", body.Notes, StringComparison.Ordinal);
        var item = Assert.Single(body.Items!);
        Assert.Equal("M2", item.ExternalProductId);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(18.00m, item.UnitPrice);
        Assert.Equal("Cheeseburger", item.Name);
    }

    [Fact]
    public void Collection_becomes_takeaway_and_a_missing_quantity_is_one()
    {
        var mapped = LieferandoOrderMapper.Map(Sample(type: "collection-by-customer", quantity: null, party: "collector"));

        Assert.Null(mapped.Error);
        var body = JsonSerializer.Deserialize<OnlineOrderIntegrationWebhookBody>(mapped.NormalizedJson!, JsonOptions())!;
        Assert.Equal("takeaway", body.OrderType);
        Assert.Equal(1, Assert.Single(body.Items!).Quantity);
    }

    [Fact]
    public void Delivery_and_service_adjustments_are_fees_in_euros()
    {
        var json = Sample(adjustments: """[{"type":"delivery","price":200},{"type":"service_fee","price":50}]""", incTax: 2050);
        var mapped = LieferandoOrderMapper.Map(json);

        Assert.Null(mapped.Error);
        var body = JsonSerializer.Deserialize<OnlineOrderIntegrationWebhookBody>(mapped.NormalizedJson!, JsonOptions())!;
        Assert.Equal(20.50m, body.ChannelTotal);
        Assert.Equal(2.00m, body.DeliveryFee);
        Assert.Equal(0.50m, body.ServiceFee);
    }

    [Fact]
    public void Tax_exclusive_sample_is_rejected_without_guessing_a_gross_price()
    {
        var mapped = LieferandoOrderMapper.Map(Sample(incTax: 2160));

        Assert.True(mapped.Parsed);
        Assert.Equal(LieferandoOrderMapper.TaxMismatchError, mapped.Error);
        Assert.Null(mapped.NormalizedJson);
    }

    [Fact]
    public void Masked_customer_is_rejected()
    {
        var mapped = LieferandoOrderMapper.Map(Sample(name: "****", phone: "****"));

        Assert.Equal(LieferandoOrderMapper.MaskedCustomerError, mapped.Error);
    }

    [Theory]
    [InlineData("""[{"type":"discount","price":-100}]""")]
    [InlineData("""[{"type":"delivery","price":-50}]""")]
    public void Discounts_are_rejected(string adjustments)
    {
        var mapped = LieferandoOrderMapper.Map(Sample(adjustments: adjustments, incTax: 1700));

        Assert.Equal(LieferandoOrderMapper.DiscountError, mapped.Error);
    }

    [Fact]
    public void Unknown_adjustment_shape_is_rejected()
    {
        var mapped = LieferandoOrderMapper.Map(Sample(adjustments: """[{"type":"other","price":50}]""", incTax: 1850));

        Assert.Equal(LieferandoOrderMapper.UnknownAdjustmentError, mapped.Error);
    }

    [Fact]
    public void Fractional_item_price_is_rejected()
    {
        var mapped = LieferandoOrderMapper.Map(Sample(price: "17.5"));

        Assert.Equal(LieferandoOrderMapper.AmbiguousPriceError, mapped.Error);
    }

    [Fact]
    public void Confirmation_flag_stays_off()
    {
        Assert.False(OnlineOrderIntegrationSignatureStatus.LieferandoConfirmed);
        Assert.Equal("lieferando", OnlineOrderIntegrationSignatureStatus.LieferandoProvider);
        Assert.False(OnlineOrderIntegrationSignatureStatus.IsConfirmed("lieferando"));
    }

    private static string Sample(
        string type = "delivery-by-delivery-partner",
        int? quantity = null,
        string party = "delivery",
        string adjustments = "[]",
        int incTax = 1800,
        string name = "Anna",
        string last = "Berger",
        string phone = "+431234567",
        string price = "1700") =>
        $$"""
        {
          "type": "{{type}}",
          "posLocationId": "AKZ12",
          "id": "38bbeb45-f520-4438-a44f-0fcdbb29e166",
          "items": [
            {
              "name": "Cheeseburger",
              "plu": "M2",
              "children": [ { "name": "Extra Sauce", "plu": "R3", "price": 100 } ],
              "price": {{price}}{{(quantity is int count ? $",\n              \"quantity\": {count}" : "")}}
            }
          ],
          "third_party_order_reference": "22721763",
          "payment_method": "CARD",
          "payment": {
            "adjustments": {{adjustments}},
            "final": { "inc_tax": {{incTax}}, "tax": 0 }
          },
          "{{party}}": {
            "first_name": "{{name}}",
            "last_name": "{{last}}",
            "phone_number": "{{phone}}",
            "line_one": "Hauptstrasse 1",
            "city": "Wien",
            "postcode": "1010"
          }
        }
        """;

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
