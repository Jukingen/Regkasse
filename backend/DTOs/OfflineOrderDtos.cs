namespace KasseAPI_Final.DTOs;

public record OfflineOrderRequest
{
    public Guid CashRegisterId { get; init; }
    public object OrderData { get; init; } = null!; // Full order
    public decimal OrderTotal { get; init; }
    public string PaymentMethod { get; init; } = null!;
}

public record OfflineOrderResponse
{
    public Guid Id { get; init; }
    public string OfflineOrderId { get; init; } = null!;
    public string Status { get; init; } = null!;
    public DateTime ExpiresAtUtc { get; init; }
    public int HoursRemaining { get; init; }
}

public record ReplayOfflineOrdersResult
{
    public int Total { get; init; }
    public int Success { get; init; }
    public int Failed { get; init; }
    public List<ReplayOfflineOrderResult> Details { get; init; } = new();
}

public record ReplayOfflineOrderResult
{
    public Guid OrderId { get; init; }
    public bool Success { get; init; }
    public string? PaymentId { get; init; }
    public string? InvoiceNumber { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class AdminOfflineOrdersListQuery
{
    public string? Status { get; set; }
    public Guid? CashRegisterId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class AdminOfflineOrderRowDto
{
    public Guid Id { get; set; }
    public string OfflineOrderId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public decimal OrderTotal { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public Guid CashRegisterId { get; set; }
    public string CashRegisterLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int HoursRemaining { get; set; }
    public int SyncAttempts { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? SyncedPaymentId { get; set; }
    public string? SyncedInvoiceNumber { get; set; }
}

public sealed class AdminOfflineOrdersListResponse
{
    public IReadOnlyList<AdminOfflineOrderRowDto> Items { get; set; } = Array.Empty<AdminOfflineOrderRowDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

/// <summary>POS sync health snapshot for offline order queue monitoring.</summary>
public sealed class PosOfflineSyncHealthDto
{
    public int PendingOrders { get; set; }
    public int MaxPending { get; set; }
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = "healthy";
    public DateTime? LastSyncAt { get; set; }
}
