namespace KasseAPI_Final.DTOs;

public sealed class OnlineOrderStatusChangeDto
{
    public Guid Id { get; init; }
    public string FromStatus { get; init; } = string.Empty;
    public string ToStatus { get; init; } = string.Empty;
    public DateTime ChangedAt { get; init; }
}

public sealed class OnlineOrderAnalyticsDto
{
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public int TotalOrders { get; init; }
    public int Pending { get; init; }
    public int Completed { get; init; }
    public int Cancelled { get; init; }
    public decimal Revenue { get; init; }
    public decimal AverageOrderValue { get; init; }
    public double? AvgAcceptToReadyMinutes { get; init; }
    public IReadOnlyDictionary<string, int> ByStatus { get; init; } =
        new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> BySource { get; init; } =
        new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> ByOrderType { get; init; } =
        new Dictionary<string, int>();
}

public sealed class LoyaltyEarnResultDto
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public Guid? CustomerId { get; init; }
    public int PointsAwarded { get; init; }
    public int LoyaltyPointsBalance { get; init; }
}
