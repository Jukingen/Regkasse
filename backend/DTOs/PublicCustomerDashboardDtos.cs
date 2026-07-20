namespace KasseAPI_Final.DTOs;

/// <summary>Anonymous customer portal snapshot (phone + tenant slug gate).</summary>
public sealed class PublicCustomerDashboardDto
{
    public string CustomerDisplayName { get; init; } = string.Empty;
    public int LoyaltyPoints { get; init; }
    public decimal RedeemableEuro { get; init; }
    public decimal TotalSpent { get; init; }
    public int TotalOrders { get; init; }
    public IReadOnlyList<PublicCustomerOrderSummaryDto> Orders { get; init; } =
        Array.Empty<PublicCustomerOrderSummaryDto>();
}

public sealed class PublicCustomerOrderSummaryDto
{
    public string OrderNumber { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public string Currency { get; init; } = "EUR";
    public DateTime CreatedAt { get; init; }
}
