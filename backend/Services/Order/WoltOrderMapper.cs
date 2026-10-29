using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Maps a Wolt Order V2 document onto the existing normalized webhook body.
/// Channel total is <c>basket_price.total</c>. Fees stay on their own fields so the
/// processor can reject a mismatch. Option prices are taken from the line total Wolt
/// already rolled up; they are not added again.
/// </summary>
public static class WoltOrderMapper
{
    public const string CurrencyError = "Currency must be EUR.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static WoltOrderMapResult Map(string orderJson)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(orderJson);
        }
        catch (JsonException)
        {
            return WoltOrderMapResult.Unreadable();
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return WoltOrderMapResult.Unreadable();

            var orderId = ReadString(root, "id");
            var venueId = root.TryGetProperty("venue", out var venue) ? ReadString(venue, "id") : null;
            if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(venueId))
                return WoltOrderMapResult.Unreadable();

            var currencies = new List<string>();
            var channelCents = ReadMoney(root, "basket_price", "total", currencies);
            var deliveryCents = 0L;
            var serviceCents = 0L;
            string? address = null;
            var orderType = "delivery";
            if (root.TryGetProperty("delivery", out var delivery) && delivery.ValueKind == JsonValueKind.Object)
            {
                deliveryCents = ReadMoney(delivery, "fee", currencies);
                serviceCents = ReadMoney(delivery, "small_order_surcharge", currencies);
                if (delivery.TryGetProperty("location", out var location))
                    address = ReadString(location, "formatted_address");
                var deliveryType = ReadString(delivery, "type");
                if (string.Equals(deliveryType, "takeaway", StringComparison.OrdinalIgnoreCase))
                    orderType = "takeaway";
            }

            var items = new List<OnlineOrderIntegrationWebhookItem>();
            if (root.TryGetProperty("items", out var itemArray) && itemArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemArray.EnumerateArray())
                {
                    var count = ReadInt(item, "count");
                    long? lineCents = null;
                    if (item.TryGetProperty("item_price", out var itemPrice) && itemPrice.ValueKind == JsonValueKind.Object)
                    {
                        if (itemPrice.TryGetProperty("total", out _))
                            lineCents = ReadMoney(itemPrice, "total", currencies);
                        else if (itemPrice.TryGetProperty("unit_price", out _))
                            lineCents = ReadMoney(itemPrice, "unit_price", currencies) * Math.Max(count, 0);
                    }

                    var unit = count > 0 && lineCents is long cents
                        ? Money(cents / (decimal)count / 100m)
                        : 0m;
                    items.Add(new OnlineOrderIntegrationWebhookItem
                    {
                        ExternalProductId = ReadString(item, "pos_id"),
                        Quantity = count,
                        UnitPrice = unit,
                        Name = ReadString(item, "name"),
                    });
                }
            }

            var currencyOk = currencies.Count > 0 && currencies.All(c => string.Equals(c, "EUR", StringComparison.OrdinalIgnoreCase));
            var body = new OnlineOrderIntegrationWebhookBody
            {
                ExternalOrderId = orderId.Trim(),
                ExternalLocationId = venueId.Trim(),
                ChannelTotal = Money(channelCents / 100m),
                DeliveryFee = Money(deliveryCents / 100m),
                ServiceFee = Money(serviceCents / 100m),
                CustomerName = ReadString(root, "consumer_name"),
                CustomerPhone = ReadString(root, "consumer_phone_number"),
                Notes = ReadString(root, "consumer_comment"),
                OrderType = orderType,
                DeliveryAddress = address,
                Items = items,
            };

            return new WoltOrderMapResult
            {
                Parsed = true,
                CurrencyAccepted = currencyOk,
                NormalizedJson = JsonSerializer.Serialize(body, JsonOptions),
                Error = currencyOk ? null : CurrencyError,
            };
        }
    }

    private static long ReadMoney(JsonElement parent, string objectName, List<string> currencies)
    {
        if (!parent.TryGetProperty(objectName, out var money) || money.ValueKind != JsonValueKind.Object)
            return 0;
        return ReadMoneyObject(money, currencies);
    }

    private static long ReadMoney(JsonElement parent, string objectName, string totalName, List<string> currencies)
    {
        if (!parent.TryGetProperty(objectName, out var holder) || holder.ValueKind != JsonValueKind.Object)
            return 0;
        if (!holder.TryGetProperty(totalName, out var money) || money.ValueKind != JsonValueKind.Object)
            return 0;
        return ReadMoneyObject(money, currencies);
    }

    private static long ReadMoneyObject(JsonElement money, List<string> currencies)
    {
        var currency = ReadString(money, "currency");
        if (!string.IsNullOrWhiteSpace(currency))
            currencies.Add(currency.Trim());
        if (!money.TryGetProperty("amount", out var amount))
            return 0;
        return amount.ValueKind switch
        {
            JsonValueKind.Number when amount.TryGetInt64(out var whole) => whole,
            JsonValueKind.String when long.TryParse(amount.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0,
        };
    }

    private static int ReadInt(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
            return 0;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            _ => 0,
        };
    }

    private static string? ReadString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed class WoltOrderMapResult
{
    public bool Parsed { get; init; }

    public bool CurrencyAccepted { get; init; }

    public string? NormalizedJson { get; init; }

    public string? Error { get; init; }

    public static WoltOrderMapResult Unreadable() => new() { Parsed = false };
}
