namespace KasseAPI_Final.DTOs;

/// <summary>Legacy TSE intent queue statistics (internal monitoring detail).</summary>
public sealed class OfflineTransactionStats
{
    public int PendingCount { get; init; }
    public int NonFiscalPendingCount { get; init; }
    public int FailedCount { get; init; }
    public int SyncedLast24Hours { get; init; }
    public int ClockDriftWarningCount { get; init; }
    public int SequenceGapCount { get; init; }
    public DateTime? LastReplayAtUtc { get; init; }
    public IReadOnlyList<OfflineRegisterQueueSummary> ByRegister { get; init; } =
        Array.Empty<OfflineRegisterQueueSummary>();
}

public sealed record OfflineRegisterQueueSummary
{
    public Guid CashRegisterId { get; init; }
    public string RegisterNumber { get; init; } = "";
    public int PendingCount { get; init; }
    public int FailedCount { get; init; }
}
