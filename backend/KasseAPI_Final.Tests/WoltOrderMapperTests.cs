using System.Text.Json;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Order;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class WoltOrderMapperTests
{
    [Fact]
    public void Maps_cents_pos_id_and_fee_lines()
    {
        var mapped = WoltOrderMapper.Map(
            """
            {
              "id": "ord-1",
              "venue": { "id": "venue-1" },
              "consumer_name": "Max Mustermann",
              "consumer_phone_number": "+431234567",
              "basket_price": { "total": { "amount": 1250, "currency": "EUR" } },
              "delivery": {
                "type": "homedelivery",
                "fee": { "amount": 200, "currency": "EUR" },
                "small_order_surcharge": { "amount": 50, "currency": "EUR" }
              },
              "items": [
                {
                  "pos_id": "plu-pizza",
                  "name": "Pizza",
                  "count": 1,
                  "item_price": { "unit_price": { "amount": 1000, "currency": "EUR" } }
                }
              ]
            }
            """);

        Assert.True(mapped.Parsed);
        Assert.True(mapped.CurrencyAccepted);
        var body = JsonSerializer.Deserialize<OnlineOrderIntegrationWebhookBody>(mapped.NormalizedJson!, JsonOptions())!;
        var item = Assert.Single(body.Items!);
        Assert.Equal("plu-pizza", item.ExternalProductId);
        Assert.Equal(10.00m, item.UnitPrice);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(2.00m, body.DeliveryFee);
        Assert.Equal(0.50m, body.ServiceFee);
        Assert.Equal(12.50m, body.ChannelTotal);
        Assert.Equal("ord-1", body.ExternalOrderId);
        Assert.Equal("venue-1", body.ExternalLocationId);
    }

    [Fact]
    public void Channel_total_stays_on_the_declared_basket_total_when_it_does_not_match_lines()
    {
        var mapped = WoltOrderMapper.Map(
            """
            {
              "id": "ord-2",
              "venue": { "id": "venue-1" },
              "basket_price": { "total": { "amount": 9999, "currency": "EUR" } },
              "items": [
                {
                  "pos_id": "plu-pizza",
                  "count": 1,
                  "item_price": { "unit_price": { "amount": 1000, "currency": "EUR" } }
                }
              ]
            }
            """);

        Assert.True(mapped.CurrencyAccepted);
        var body = JsonSerializer.Deserialize<OnlineOrderIntegrationWebhookBody>(mapped.NormalizedJson!, JsonOptions())!;
        Assert.Equal(99.99m, body.ChannelTotal);
        Assert.Equal(10.00m, Assert.Single(body.Items!).UnitPrice);
        Assert.NotEqual(body.ChannelTotal, body.Items![0].UnitPrice + body.DeliveryFee + body.ServiceFee);
    }

    [Fact]
    public void Non_eur_currency_is_rejected()
    {
        var mapped = WoltOrderMapper.Map(
            """
            {
              "id": "ord-3",
              "venue": { "id": "venue-1" },
              "basket_price": { "total": { "amount": 1000, "currency": "USD" } },
              "items": [
                {
                  "pos_id": "plu-pizza",
                  "count": 1,
                  "item_price": { "unit_price": { "amount": 1000, "currency": "USD" } }
                }
              ]
            }
            """);

        Assert.True(mapped.Parsed);
        Assert.False(mapped.CurrencyAccepted);
        Assert.Equal(WoltOrderMapper.CurrencyError, mapped.Error);
        Assert.False(string.IsNullOrWhiteSpace(mapped.NormalizedJson));
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
