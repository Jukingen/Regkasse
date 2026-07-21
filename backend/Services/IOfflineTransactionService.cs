using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services
{
    public interface IOfflineTransactionService
    {
        /// <summary>
        /// Replays controlled offline intents in the provided order.
        /// Invariant: OfflineTransaction rows themselves never generate receipts/signatures.
        /// </summary>
        Task<ReplayOfflineTransactionsResponse> ReplayOfflineTransactionsAsync(
            ReplayOfflineTransactionsRequest request,
            string userId,
            string userRole);
    }

    public sealed class ReplayOfflineTransactionsResponse
    {
        /// <summary>Server-generated id for this POST /replay call; ties audits, logs, and payment rows together.</summary>
        public Guid? ReplayBatchCorrelationId { get; set; }

        public IReadOnlyList<ReplayOfflineTransactionsResponseItem> Items { get; set; } =
            Array.Empty<ReplayOfflineTransactionsResponseItem>();
    }
}

