namespace KasseAPI_Final.Services;

/// <summary>
/// Hook for notifying POS clients when server-side offline replay finishes (replace with push/WebSocket when available).
/// </summary>
public interface IOfflineReplayCompletionNotifier
{
    Task NotifyReplayBatchCompletedAsync(
        Guid cashRegisterId,
        int processedCount,
        CancellationToken cancellationToken = default);
}
