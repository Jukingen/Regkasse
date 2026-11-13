namespace KasseAPI_Final.DTOs;

/// <summary>Tenant-scoped TSE NonFiscalPending offline intent queue status.</summary>
public sealed class TseOfflineQueueStatusDto
{
    public Guid TenantId { get; set; }
    public int TotalQueued { get; set; }
    public int CriticalThreshold { get; set; }
    public int WarningThreshold { get; set; }
    public int MaxPerRegister { get; set; }
    public bool IsCritical { get; set; }
    public bool IsWarning { get; set; }
    public DateTime? OldestTransaction { get; set; }
    public DateTime? NewestTransaction { get; set; }
    public IReadOnlyList<TseOfflineQueueRegisterSummaryDto> ByRegister { get; set; } =
        Array.Empty<TseOfflineQueueRegisterSummaryDto>();
}

public sealed class TseOfflineQueueRegisterSummaryDto
{
    public Guid CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }
    public int QueuedCount { get; set; }
    public int MaxPerRegister { get; set; }
    public bool IsAtCap { get; set; }
    public bool IsNearCap { get; set; }
}

public sealed class TseOfflineQueuedTransactionDto
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public string? CashRegisterLabel { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ServerReceivedAtUtc { get; set; }
    public DateTime OfflineCreatedAtUtc { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "unknown";
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public string? DeviceId { get; set; }
}

public sealed class TseOfflineQueueClearResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int SoftClearedCount { get; set; }
    public string? Detail { get; set; }
}

public sealed class TseOfflineQueueClearRequestDto
{
    /// <summary>Must equal <c>SOFT_CLEAR</c>.</summary>
    public string ConfirmToken { get; set; } = string.Empty;

    public string? Reason { get; set; }
}

public sealed class TseOfflineQueueAlertResultDto
{
    public bool Sent { get; set; }
    public string Severity { get; set; } = "Info";
    public string? Message { get; set; }
}
