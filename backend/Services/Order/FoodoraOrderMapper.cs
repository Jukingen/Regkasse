using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Maps a Foodora Partner Picking order onto the existing normalized webhook body.
/// Uniqueness key is <c>order_id</c>. The JSON field <c>external_order_id</c> is Foodora's
/// platform number and is kept on the note only. Catalog match uses <c>sku</c>.
/// </summary>
public static class FoodoraOrderMapper
{
    public const string CountryError = "Country must be AT.";

    public const string LogisticsDelivery = "LOGISTICS_DELIVERY";

    public const string VendorDelivery = "VENDOR_DELIVERY";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string? MapKitchenStatus(string? foodoraStatus)
    {
        var status = (foodoraStatus ?? string.Empty).Trim();
        if (status.Equals("RECEIVED", StringComparison.OrdinalIgnoreCase))
            return OnlineOrderStatuses.Pending;
        if (status.Equals("READY_FOR_PICKUP", StringComparison.OrdinalIgnoreCase))
            return OnlineOrderStatuses.Ready;
        if (status.Equals("DISPATCHED", StringComparison.OrdinalIgnoreCase))
            return OnlineOrderStatuses.Completed;
        if (status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)
            || status.Equals("CANCELED", StringComparison.OrdinalIgnoreCase))
            return OnlineOrderStatuses.Cancelled;
        return null;
    }

    public static FoodoraOrderMapResult Map(string orderJson)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(orderJson);
        }
        catch (JsonException)
        {
            return FoodoraOrderMapResult.Unreadable();
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return FoodoraOrderMapResult.Unreadable();

            var orderId = ReadString(root, "order_id");
            var client = root.TryGetProperty("client", out var clientElement) && clientElement.ValueKind == JsonValueKind.Object
                ? clientElement
                : default;
            var locationId = client.ValueKind == JsonValueKind.Object ? ReadString(client, "id") : null;
            var chainId = client.ValueKind == JsonValueKind.Object ? ReadString(client, "chain_id") : null;
            if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(locationId))
                return FoodoraOrderMapResult.Unreadable();

            var country = client.ValueKind == JsonValueKind.Object
                ? ReadString(client, "country_code")
                : null;
            var countryAccepted = string.Equals(country?.Trim(), "at", StringComparison.OrdinalIgnoreCase);
            var foodoraStatus = ReadString(root, "status");
            var transport = ReadString(root, "transport_type") ?? LogisticsDelivery;
            var platformOrderId = ReadString(root, "external_order_id");
            var orderCode = ReadString(root, "order_code");

            decimal orderTotal = 0m;
            decimal deliveryFee = 0m;
            decimal serviceFee = 0m;
            if (root.TryGetProperty("payment", out var payment) && payment.ValueKind == JsonValueKind.Object)
            {
                orderTotal = ReadDecimal(payment, "order_total");
                deliveryFee = ReadDecimal(payment, "delivery_fee");
                serviceFee = ReadDecimal(payment, "service_fee");
            }

            var items = new List<OnlineOrderIntegrationWebhookItem>();
            var itemsPresent = root.TryGetProperty("items", out var itemArray) && itemArray.ValueKind == JsonValueKind.Array;
            if (itemsPresent)
            {
                foreach (var item in itemArray.EnumerateArray())
                {
                    var quantity = ReadQuantity(item);
                    var unit = ReadUnitPrice(item, quantity);
                    items.Add(new OnlineOrderIntegrationWebhookItem
                    {
                        ExternalProductId = ReadString(item, "sku"),
                        Quantity = quantity,
                        UnitPrice = Money(unit),
                        Name = ReadString(item, "name"),
                    });
                }
            }

            var (customerName, customerPhone) = ReadCustomer(root);
            var notes = BuildNotes(orderCode, platformOrderId, ReadString(root, "comment"));
            var body = new OnlineOrderIntegrationWebhookBody
            {
                ExternalOrderId = orderId.Trim(),
                ExternalLocationId = locationId.Trim(),
                ExternalChannel = "foodora",
                ChannelTotal = Money(orderTotal),
                DeliveryFee = Money(deliveryFee),
                ServiceFee = Money(serviceFee),
                CustomerName = customerName,
                CustomerPhone = customerPhone,
                Notes = notes,
                OrderType = "delivery",
                Items = items,
            };

            return new FoodoraOrderMapResult
            {
                Parsed = true,
                CountryAccepted = countryAccepted,
                OrderId = orderId.Trim(),
                PlatformOrderId = platformOrderId,
                LocationId = locationId.Trim(),
                ChainId = chainId?.Trim(),
                FoodoraStatus = foodoraStatus,
                KitchenStatus = MapKitchenStatus(foodoraStatus),
                TransportType = transport.Trim(),
                ItemsPresent = items.Count > 0,
                NormalizedJson = JsonSerializer.Serialize(body, JsonOptions),
                Error = countryAccepted ? null : CountryError,
            };
        }
    }

    internal static string? ReadStatus(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            return ReadString(document.RootElement, "status");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (string? Name, string? Phone) ReadCustomer(JsonElement root)
    {
        if (!root.TryGetProperty("customer", out var customer) || customer.ValueKind != JsonValueKind.Object)
            return (null, null);

        var name = ReadString(customer, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            var first = ReadString(customer, "first_name");
            var last = ReadString(customer, "last_name");
            name = string.Join(' ', new[] { first, last }.Where(part => !string.IsNullOrWhiteSpace(part)));
            if (string.IsNullOrWhiteSpace(name))
                name = null;
        }

        var phone = ReadString(customer, "phone") ?? ReadString(customer, "phone_number");
        return (name, phone);
    }

    private static string? BuildNotes(string? orderCode, string? platformOrderId, string? comment)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(orderCode))
            parts.Add("order_code " + orderCode.Trim());
        if (!string.IsNullOrWhiteSpace(platformOrderId))
            parts.Add("external_order_id " + platformOrderId.Trim());
        if (!string.IsNullOrWhiteSpace(comment))
            parts.Add(comment.Trim());
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static int ReadQuantity(JsonElement item)
    {
        var direct = ReadInt(item, "quantity");
        if (direct > 0)
            return direct;
        if (item.TryGetProperty("pricing", out var pricing) && pricing.ValueKind == JsonValueKind.Object)
        {
            var nested = ReadInt(pricing, "quantity");
            if (nested > 0)
                return nested;
        }

        return 0;
    }

    private static decimal ReadUnitPrice(JsonElement item, int quantity)
    {
        var direct = ReadDecimal(item, "unit_price");
        if (direct > 0)
            return direct;
        if (item.TryGetProperty("pricing", out var pricing) && pricing.ValueKind == JsonValueKind.Object)
        {
            var nested = ReadDecimal(pricing, "unit_price");
            if (nested > 0)
                return nested;
            var total = ReadDecimal(pricing, "total_price");
            if (total > 0 && quantity > 0)
                return total / quantity;
        }

        return 0m;
    }

    private static decimal ReadDecimal(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
            return 0m;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0m,
        };
    }

    private static int ReadInt(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
            return 0;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0,
        };
    }

    private static string? ReadString(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed class FoodoraOrderMapResult
{
    public bool Parsed { get; init; }

    public bool CountryAccepted { get; init; }

    public string? OrderId { get; init; }

    /// <summary>Foodora platform number. Not the uniqueness key.</summary>
    public string? PlatformOrderId { get; init; }

    public string? LocationId { get; init; }

    public string? ChainId { get; init; }

    public string? FoodoraStatus { get; init; }

    public string? KitchenStatus { get; init; }

    public string? TransportType { get; init; }

    public bool ItemsPresent { get; init; }

    public string? NormalizedJson { get; init; }

    public string? Error { get; init; }

    public static FoodoraOrderMapResult Unreadable() => new() { Parsed = false };
}
