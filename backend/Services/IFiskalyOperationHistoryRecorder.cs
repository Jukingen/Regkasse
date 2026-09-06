namespace KasseAPI_Final.Services;

public sealed class FiskalyOperationHistoryStartRequest
{
    public string OperationType { get; init; } = string.Empty;

    public Guid CashRegisterId { get; init; }

    public string ActorUserId { get; init; } = string.Empty;

    public object? RequestPayload { get; init; }
}

public sealed class FiskalyOperationHistoryCompleteRequest
{
    public bool Success { get; init; }

    public string? ReceiptNumber { get; init; }

    public string? ReceiptId { get; init; }

    public object? ResponsePayload { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}

/// <summary>Writes Fiskaly operation history rows from the receipt facade (no retry).</summary>
public interface IFiskalyOperationHistoryRecorder
{
    Task<Guid?> StartAsync(FiskalyOperationHistoryStartRequest request, CancellationToken cancellationToken = default);

    Task MarkProcessingAsync(Guid historyId, CancellationToken cancellationToken = default);

    Task CompleteAsync(Guid historyId, FiskalyOperationHistoryCompleteRequest request, CancellationToken cancellationToken = default);
}
