namespace KasseAPI_Final.DTOs;

public sealed class OnlineOrderListResponseDto
{
    public int Pending { get; init; }
    public int Accepted { get; init; }
    public int Preparing { get; init; }
    public int Ready { get; init; }
    public int Completed { get; init; }
    public IReadOnlyList<OnlineOrderDto> Orders { get; init; } = Array.Empty<OnlineOrderDto>();
}

public sealed class OnlineOrderDto
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public string? CustomerEmail { get; init; }
    public string OrderType { get; init; } = string.Empty;
    public string? TableNumber { get; init; }
    public string? DeliveryAddress { get; init; }
    public decimal Subtotal { get; init; }
    public decimal Tax { get; init; }
    public decimal Total { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public DateTime? PaidAt { get; init; }
    public string? StripePaymentIntentId { get; init; }
    public string OrderStatus { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? AcceptedAt { get; init; }
    public DateTime? ReadyAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? Notes { get; init; }
    public string? PosCartId { get; init; }
    public Guid? CustomerId { get; init; }
    public IReadOnlyList<OnlineOrderItemDto> Items { get; init; } = Array.Empty<OnlineOrderItemDto>();
    public IReadOnlyList<OnlineOrderStatusChangeDto> StatusHistory { get; init; } =
        Array.Empty<OnlineOrderStatusChangeDto>();
}

public sealed class OnlineOrderItemDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal Price { get; init; }
    public decimal Total { get; init; }
    public IReadOnlyList<OnlineOrderItemModifierDto> Modifiers { get; init; } =
        Array.Empty<OnlineOrderItemModifierDto>();
}

public sealed class OnlineOrderItemModifierDto
{
    public Guid Id { get; init; }
    public Guid? ModifierId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Quantity { get; init; }
}

public sealed record AcceptOnlineOrderResponseDto
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public string? PosCartId { get; init; }
    public bool AlreadyPushed { get; init; }
    public OnlineOrderDto? Order { get; init; }
}
