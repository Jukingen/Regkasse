using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace KasseAPI_Final.DTOs
{
    /// <summary>
    /// Replay request for controlled offline intents.
    /// Payload is the original CreatePaymentRequest JSON as sent by the mobile POS.
    /// </summary>
    public sealed class ReplayOfflineTransactionsRequest
    {
        [Required]
        public List<ReplayOfflineTransactionItem> Transactions { get; set; } = new();
    }

    public sealed class ReplayOfflineTransactionItem
    {
        [Required]
        public Guid OfflineTransactionId { get; set; }

        [Required]
        public DateTime CreatedAtUtc { get; set; }

        [Required]
        public Guid CashRegisterId { get; set; }

        [Required]
        public JsonElement Payload { get; set; }

        /// <summary>
        /// Optional device id used for monotonic client sequence tracking.
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Optional client sequence number used for monotonic client sequence tracking.
        /// </summary>
        public int? ClientSequenceNumber { get; set; }
    }

    public sealed class ReplayOfflineTransactionsResponseItem
    {
        /// <summary>
        /// The OfflineTransactionId originally sent by the client for this queue entry.
        /// Client should update its local state based on this id.
        /// </summary>
        public Guid RequestedOfflineTransactionId { get; set; }

        /// <summary>
        /// The canonical OfflineTransaction.Id after dedup / conflict resolution.
        /// </summary>
        public Guid OfflineTransactionId { get; set; }

        public string Status { get; set; } = string.Empty;
        public Guid? SyncedPaymentId { get; set; }
        public string? Error { get; set; }
        public string? ErrorCode { get; set; }
        public int RetryCount { get; set; }
        public string? LastErrorMessageSafe { get; set; }

        /// <summary>
        /// When Status stays Pending (under retry limit), hint next retry delay in seconds.
        /// </summary>
        public int? ExponentialBackoffHintSeconds { get; set; }

        /// <summary>Server-generated replay batch id (same for all items in one POST /replay).</summary>
        public Guid ReplayBatchCorrelationId { get; set; }
    }
}

