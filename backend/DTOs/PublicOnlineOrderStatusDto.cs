namespace KasseAPI_Final.DTOs;

/// <summary>Public customer-facing order status (limited PII).</summary>
public sealed class PublicOnlineOrderStatusDto
{
    public required string OrderNumber { get; init; }
    public required string OrderStatus { get; init; }
    public required string OrderType { get; init; }
    public string PaymentStatus { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public string Currency { get; init; } = "EUR";
    public DateTime CreatedAt { get; init; }
    public DateTime? AcceptedAt { get; init; }
    public DateTime? ReadyAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? PaidAt { get; init; }
    /// <summary>Masked customer name (e.g. "Max M.").</summary>
    public string? CustomerDisplayName { get; init; }
    public IReadOnlyList<OnlineOrderStatusChangeDto> StatusHistory { get; init; } =
        Array.Empty<OnlineOrderStatusChangeDto>();
}
