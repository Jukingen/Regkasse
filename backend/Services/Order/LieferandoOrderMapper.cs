using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Maps a JET Connect Receive Order document onto the normalized webhook body.
/// Minor-unit integers are cents. A fractional amount, a discount, or a masked
/// customer fails the row instead of guessing a price or a contact.
/// </summary>
public static class LieferandoOrderMapper
{
    public const string MaskedCustomerError = "Customer name or phone is masked.";

    public const string TaxMismatchError =
        "Item prices do not equal payment.final.inc_tax. Tax treatment is unconfirmed.";

    public const string DiscountError = "Payment adjustment is a discount.";

    public const string UnknownAdjustmentError = "Payment adjustment type is unknown.";

    public const string AmbiguousAmountError = "Payment adjustment amount is ambiguous.";

    public const string MissingTotalError = "Channel total is missing.";

    public const string MissingPluError = "Item PLU is missing.";

    public const string MissingPriceError = "Item price is missing.";

    public const string InvalidQuantityError = "Item quantity is invalid.";

    public const string UnevenPriceError = "Item price does not divide evenly by quantity.";

    public const string AmbiguousPriceError = "Item price scale is ambiguous.";

    public const string MissingChildPriceError = "Child price is missing.";

    public const string NegativeChildPriceError = "Child price is negative.";

    public const string ChildrenUnreadableError = "Item children could not be read.";

    public const string UnsupportedTypeError = "Fulfillment type is not supported.";

    public const string PromotionsError = "Promotions are not mapped.";

    public const string CurrencyError = "Currency must be EUR.";

    public const string AdjustmentsUnreadableError = "Payment adjustments could not be read.";

    public const string RejectedCode = "LIEFERANDO_PAYLOAD_REJECTED";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static LieferandoOrderMapResult Map(string orderJson)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(orderJson);
        }
        catch (JsonException)
        {
            return LieferandoOrderMapResult.Unreadable();
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return LieferandoOrderMapResult.Unreadable();

            var orderId = ReadString(root, "id");
            var locationId = ReadString(root, "posLocationId");
            if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(locationId))
                return LieferandoOrderMapResult.Unreadable();

            var orderType = MapOrderType(ReadString(root, "type"));
            if (orderType is null)
                return Fail(orderId, locationId, UnsupportedTypeError);

            if (HasNonEurCurrency(root))
                return Fail(orderId, locationId, CurrencyError);

            if (root.TryGetProperty("promotions", out var promotions)
                && promotions.ValueKind == JsonValueKind.Array
                && promotions.GetArrayLength() > 0)
                return Fail(orderId, locationId, PromotionsError);

            if (!root.TryGetProperty("payment", out var payment) || payment.ValueKind != JsonValueKind.Object)
                return Fail(orderId, locationId, MissingTotalError);

            if (!TryReadMinor(payment, "final", "inc_tax", out var channelCents, out var totalAmbiguous) || totalAmbiguous)
                return Fail(orderId, locationId, totalAmbiguous ? AmbiguousPriceError : MissingTotalError);
            if (channelCents < 0)
                return Fail(orderId, locationId, DiscountError);

            if (!TryReadFees(payment, out var deliveryCents, out var serviceCents, out var feeError))
                return Fail(orderId, locationId, feeError ?? AdjustmentsUnreadableError);

            if (!TryReadItems(root, out var items, out var itemCents, out var itemError))
                return Fail(orderId, locationId, itemError ?? MissingPluError);

            if (itemCents + deliveryCents + serviceCents != channelCents)
                return Fail(orderId, locationId, TaxMismatchError);

            if (!TryReadCustomer(root, orderType, out var name, out var phone, out var email, out var address))
                return Fail(orderId, locationId, MaskedCustomerError);

            var body = new OnlineOrderIntegrationWebhookBody
            {
                ExternalOrderId = orderId.Trim(),
                ExternalLocationId = locationId.Trim(),
                ChannelTotal = Money(channelCents / 100m),
                DeliveryFee = Money(deliveryCents / 100m),
                ServiceFee = Money(serviceCents / 100m),
                CustomerName = name,
                CustomerPhone = phone,
                CustomerEmail = email,
                OrderType = orderType,
                DeliveryAddress = address,
                Notes = BuildNotes(root),
                Items = items,
            };

            return new LieferandoOrderMapResult
            {
                Parsed = true,
                OrderId = orderId.Trim(),
                LocationId = locationId.Trim(),
                NormalizedJson = JsonSerializer.Serialize(body, JsonOptions),
            };
        }
    }

    private static bool TryReadItems(
        JsonElement root,
        out List<OnlineOrderIntegrationWebhookItem> items,
        out long itemCents,
        out string? error)
    {
        items = new List<OnlineOrderIntegrationWebhookItem>();
        itemCents = 0;
        error = null;
        if (!root.TryGetProperty("items", out var array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0)
        {
            error = MissingPluError;
            return false;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                error = MissingPluError;
                return false;
            }

            var plu = ReadString(item, "plu");
            if (string.IsNullOrWhiteSpace(plu))
            {
                error = MissingPluError;
                return false;
            }

            if (!TryReadQuantity(item, out var quantity, out var quantityError))
            {
                error = quantityError;
                return false;
            }

            if (!TryReadItemCents(item, out var lineCents, out var priceError))
            {
                error = priceError;
                return false;
            }

            if (lineCents % quantity != 0)
            {
                error = UnevenPriceError;
                return false;
            }

            itemCents += lineCents;
            items.Add(new OnlineOrderIntegrationWebhookItem
            {
                ExternalProductId = plu.Trim(),
                Quantity = quantity,
                UnitPrice = Money(lineCents / (decimal)quantity / 100m),
                Name = ReadString(item, "name"),
            });
        }

        return true;
    }

    private static bool TryReadQuantity(JsonElement item, out int quantity, out string? error)
    {
        quantity = 1;
        error = null;
        if (!item.TryGetProperty("quantity", out var value))
            return true;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var whole))
        {
            if (value.TryGetDecimal(out var dec) && dec != whole)
            {
                error = InvalidQuantityError;
                return false;
            }

            if (whole < 1)
            {
                error = InvalidQuantityError;
                return false;
            }

            quantity = whole;
            return true;
        }

        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= 1)
        {
            quantity = parsed;
            return true;
        }

        error = InvalidQuantityError;
        return false;
    }

    private static bool TryReadItemCents(JsonElement item, out long cents, out string? error)
    {
        cents = 0;
        error = null;
        if (!TryReadMinor(item, "price", out var parent, out var ambiguous))
        {
            error = ambiguous ? AmbiguousPriceError : MissingPriceError;
            return false;
        }

        if (parent < 0)
        {
            error = DiscountError;
            return false;
        }

        cents = parent;
        if (!item.TryGetProperty("children", out var children))
            return true;
        if (children.ValueKind != JsonValueKind.Array)
        {
            error = ChildrenUnreadableError;
            return false;
        }

        foreach (var child in children.EnumerateArray())
        {
            if (child.ValueKind != JsonValueKind.Object)
            {
                error = ChildrenUnreadableError;
                return false;
            }

            if (!TryReadMinor(child, "price", out var childCents, out var childAmbiguous))
            {
                error = childAmbiguous ? AmbiguousPriceError : MissingChildPriceError;
                return false;
            }

            if (childCents < 0)
            {
                error = NegativeChildPriceError;
                return false;
            }

            cents += childCents;
        }

        return true;
    }

    private static bool TryReadFees(JsonElement payment, out long deliveryCents, out long serviceCents, out string? error)
    {
        deliveryCents = 0;
        serviceCents = 0;
        error = null;
        if (!payment.TryGetProperty("adjustments", out var adjustments))
            return true;
        if (adjustments.ValueKind != JsonValueKind.Array)
        {
            error = AdjustmentsUnreadableError;
            return false;
        }

        foreach (var adjustment in adjustments.EnumerateArray())
        {
            if (adjustment.ValueKind != JsonValueKind.Object)
            {
                error = AdjustmentsUnreadableError;
                return false;
            }

            var kind = ClassifyAdjustment(ReadString(adjustment, "type"));
            if (kind is null)
            {
                error = UnknownAdjustmentError;
                return false;
            }

            if (kind == "discount")
            {
                error = DiscountError;
                return false;
            }

            if (!TryReadAdjustmentCents(adjustment, out var amount, out var amountError))
            {
                error = amountError;
                return false;
            }

            if (amount < 0)
            {
                error = DiscountError;
                return false;
            }

            if (kind == "delivery")
                deliveryCents += amount;
            else
                serviceCents += amount;
        }

        return true;
    }

    private static bool TryReadAdjustmentCents(JsonElement adjustment, out long cents, out string? error)
    {
        cents = 0;
        error = null;
        long? seen = null;
        foreach (var name in new[] { "price", "amount", "inc_tax" })
        {
            if (!adjustment.TryGetProperty(name, out _))
                continue;
            if (!TryReadMinor(adjustment, name, out var value, out var ambiguous))
            {
                error = ambiguous ? AmbiguousAmountError : AmbiguousAmountError;
                return false;
            }

            if (seen is long previous && previous != value)
            {
                error = AmbiguousAmountError;
                return false;
            }

            seen = value;
        }

        if (seen is null)
        {
            error = AmbiguousAmountError;
            return false;
        }

        cents = seen.Value;
        return true;
    }

    private static bool TryReadMinor(JsonElement parent, string objectName, string amountName, out long cents, out bool ambiguous)
    {
        cents = 0;
        ambiguous = false;
        if (!parent.TryGetProperty(objectName, out var holder) || holder.ValueKind != JsonValueKind.Object)
            return false;
        return TryReadMinor(holder, amountName, out cents, out ambiguous);
    }

    private static bool TryReadMinor(JsonElement parent, string name, out long cents, out bool ambiguous)
    {
        cents = 0;
        ambiguous = false;
        if (!parent.TryGetProperty(name, out var value))
            return false;

        if (value.ValueKind == JsonValueKind.Number)
        {
            if (value.TryGetInt64(out var whole) && value.TryGetDecimal(out var dec) && dec == whole)
            {
                cents = whole;
                return true;
            }

            ambiguous = true;
            return false;
        }

        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            cents = parsed;
            return true;
        }

        ambiguous = true;
        return false;
    }

    private static bool TryReadCustomer(
        JsonElement root,
        string orderType,
        out string? name,
        out string? phone,
        out string? email,
        out string? address)
    {
        name = null;
        phone = null;
        email = null;
        address = null;
        var partyName = orderType == "takeaway" ? "collector" : "delivery";
        if (!root.TryGetProperty(partyName, out var party) || party.ValueKind != JsonValueKind.Object)
            return false;

        var first = ReadString(party, "first_name");
        var last = ReadString(party, "last_name");
        var combined = string.Join(' ', new[] { first, last }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var phoneRaw = ReadString(party, "phone_number") ?? ReadString(party, "phone");
        if (IsMasked(combined) || combined.Trim().Length < 2 || IsMasked(phoneRaw) || CountDigits(phoneRaw) < 6)
            return false;

        name = combined.Trim();
        phone = phoneRaw!.Trim();
        var mail = ReadString(party, "email");
        email = IsMasked(mail) ? null : mail;
        address = BuildAddress(party);
        return true;
    }

    private static string? BuildAddress(JsonElement party)
    {
        var parts = new[]
        {
            ReadString(party, "line_one"),
            ReadString(party, "line_two"),
            ReadString(party, "postcode"),
            ReadString(party, "city"),
        };
        var visible = parts.Where(part => !string.IsNullOrWhiteSpace(part) && !IsMasked(part)).ToList();
        return visible.Count == 0 ? null : string.Join(", ", visible);
    }

    private static string? BuildNotes(JsonElement root)
    {
        var parts = new List<string>();
        var reference = ReadString(root, "third_party_order_reference");
        if (!string.IsNullOrWhiteSpace(reference))
            parts.Add("third_party_order_reference " + reference.Trim());
        var kitchen = ReadString(root, "kitchen_notes");
        if (!string.IsNullOrWhiteSpace(kitchen))
            parts.Add(kitchen.Trim());
        var method = ReadString(root, "payment_method");
        if (!string.IsNullOrWhiteSpace(method))
            parts.Add("payment_method " + method.Trim());
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string? MapOrderType(string? type)
    {
        if (string.Equals(type, "delivery-by-delivery-partner", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "delivery-by-merchant", StringComparison.OrdinalIgnoreCase))
            return "delivery";
        if (string.Equals(type, "collection-by-customer", StringComparison.OrdinalIgnoreCase))
            return "takeaway";
        return null;
    }

    private static string? ClassifyAdjustment(string? type)
    {
        var value = (type ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_');
        return value switch
        {
            "delivery" or "delivery_fee" or "deliveryfee" => "delivery",
            "service" or "service_fee" or "servicefee" or "service_charge" => "service",
            "discount" or "voucher" or "promotion" => "discount",
            _ => null,
        };
    }

    private static bool HasNonEurCurrency(JsonElement root)
    {
        if (CurrencyRejected(root))
            return true;
        if (root.TryGetProperty("payment", out var payment) && payment.ValueKind == JsonValueKind.Object)
        {
            if (CurrencyRejected(payment))
                return true;
            if (payment.TryGetProperty("final", out var final) && final.ValueKind == JsonValueKind.Object && CurrencyRejected(final))
                return true;
        }

        return false;
    }

    private static bool CurrencyRejected(JsonElement parent)
    {
        var currency = ReadString(parent, "currency");
        return currency is not null && !string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMasked(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;
        return value.Contains('*', StringComparison.Ordinal);
    }

    private static int CountDigits(string? value) =>
        string.IsNullOrEmpty(value) ? 0 : value.Count(char.IsDigit);

    private static string? ReadString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static LieferandoOrderMapResult Fail(string orderId, string locationId, string error) =>
        new()
        {
            Parsed = true,
            OrderId = orderId.Trim(),
            LocationId = locationId.Trim(),
            Error = error,
        };
}

public sealed class LieferandoOrderMapResult
{
    public bool Parsed { get; init; }

    public string? Error { get; init; }

    public string? OrderId { get; init; }

    public string? LocationId { get; init; }

    public string? NormalizedJson { get; init; }

    public static LieferandoOrderMapResult Unreadable() => new();
}
