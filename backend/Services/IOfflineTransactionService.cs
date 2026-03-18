using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        public IReadOnlyList<ReplayOfflineTransactionsResponseItem> Items { get; set; } =
            Array.Empty<ReplayOfflineTransactionsResponseItem>();
    }
}

