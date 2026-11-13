using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Kitchen;

public static class KitchenOrderErrorCodes
{
    public const string Empty = "KITCHEN_ORDER_EMPTY";
    public const string AlreadyServed = "KITCHEN_ORDER_ALREADY_SERVED";
    public const string Cancelled = "KITCHEN_ORDER_CANCELLED";
    public const string CashRegisterRequired = "CASH_REGISTER_REQUIRED";
    public const string InvalidStatus = "KITCHEN_ORDER_INVALID_STATUS";
}

public sealed class KitchenOrderItemDto
{
    public Guid Id { get; init; }
    public Guid? ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string? Notes { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public KitchenOrderItemStatus Status { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class KitchenOrderDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid? CartId { get; init; }
    public string? TableNumber { get; init; }
    public Guid CashRegisterId { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public KitchenOrderStatus Status { get; init; }

    public int Priority { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public DateTime? ReadyAtUtc { get; init; }
    public DateTime? ServedAtUtc { get; init; }
    public IReadOnlyList<KitchenOrderItemDto> Items { get; init; } = [];
}

public sealed class CreateKitchenOrderItemRequest
{
    public Guid? ProductId { get; set; }

    [MaxLength(255)]
    public string? ProductName { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; } = 1;

    public string? Notes { get; set; }
}

public sealed class CreateKitchenOrderRequest
{
    public Guid? CartId { get; set; }

    [MaxLength(16)]
    public string? TableNumber { get; set; }

    public Guid CashRegisterId { get; set; }

    public int Priority { get; set; }

    public string? Notes { get; set; }

    public List<CreateKitchenOrderItemRequest> Items { get; set; } = [];
}

public sealed class UpdateKitchenOrderStatusRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public KitchenOrderStatus Status { get; set; }
}

public sealed class UpdateKitchenOrderItemStatusRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public KitchenOrderItemStatus Status { get; set; }
}

public sealed class KitchenOrderErrorDto
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public abstract record KitchenOrderWriteResult
{
    public sealed record Ok(KitchenOrderDto Order, int StatusCode) : KitchenOrderWriteResult;
    public sealed record NotFound : KitchenOrderWriteResult;
    public sealed record Error(int StatusCode, string Code, string Message) : KitchenOrderWriteResult;
}
