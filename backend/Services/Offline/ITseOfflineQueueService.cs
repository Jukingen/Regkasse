using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Offline;

/// <summary>
/// Monitoring + soft-clear for the legacy TSE offline intent queue
/// (<c>offline_transactions</c> with <c>NonFiscalPending</c>). Not <c>offline_orders</c>.
/// </summary>
public interface ITseOfflineQueueService
{
    Task<TseOfflineQueueStatusDto> GetQueueStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseOfflineQueuedTransactionDto>> GetQueuedTransactionsAsync(
        Guid tenantId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-clears NonFiscalPending intents only (marks Failed). Never deletes fiscal/synced rows.
    /// Requires confirmToken <c>SOFT_CLEAR</c>.
    /// </summary>
    Task<TseOfflineQueueClearResultDto> SoftClearQueueAsync(
        Guid tenantId,
        string confirmToken,
        string? reason,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<TseOfflineQueueAlertResultDto> SendQueueAlertAsync(
        Guid tenantId,
        int? queueSize = null,
        CancellationToken cancellationToken = default);
}
