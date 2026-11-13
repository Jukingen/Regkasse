namespace KasseAPI_Final.DTOs;

public class FiskalyOperationHistoryListItemDto
{
    public Guid Id { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public string OperationType { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public Guid CashRegisterId { get; init; }

    public string? CashRegisterName { get; init; }

    public string? ReceiptNumber { get; init; }

    public string? ReceiptId { get; init; }

    public string UserId { get; init; } = string.Empty;

    public string? UserDisplayName { get; init; }

    public Guid TenantId { get; init; }

    public string? TenantName { get; init; }

    public Guid? RetriedFromId { get; init; }

    public int RetryCount { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>0–100 derived from <see cref="Status"/> (Pending 5, Processing 55, terminal 100).</summary>
    public int ProgressPercent { get; init; }
}

public sealed class FiskalyOperationHistoryDetailDto : FiskalyOperationHistoryListItemDto
{
    public string? RequestPayloadJson { get; init; }

    public string? ResponsePayloadJson { get; init; }
}

public sealed class FiskalyOperationHistoryRetryResultDto
{
    public FiskalyReceiptEnvelopeDto Operation { get; init; } = new();

    public FiskalyOperationHistoryDetailDto? History { get; init; }
}
