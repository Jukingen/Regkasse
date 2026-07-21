using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Controlled offline intent record.
    /// Invariant: OfflineTransaction itself must never get fiscal fields (receipt number/signature).
    /// A SyncedPaymentId links the offline intent to the canonical fiscal payment after replay.
    /// </summary>
    public class OfflineTransaction : BaseTenantEntity
    {
        [ForeignKey(nameof(TenantId))]
        public virtual Tenant? Tenant { get; set; }

        [Required]
        public Guid CashRegisterId { get; set; }

        /// <summary>
        /// Serialized original request payload (e.g. CreatePaymentRequest JSON).
        /// Immutable after first persist; structural mismatch on replay is rejected.
        /// Stored as normalized JSON to make hashing + dedup deterministic.
        /// </summary>
        [Required]
        [Column(TypeName = "jsonb")]
        public string PayloadJson { get; set; } = "{}";

        /// <summary>
        /// Data-protection ciphertext (base64) of UTF-8 full canonical PayloadJson when voucher plaintext was redacted from <see cref="PayloadJson"/>.
        /// Null for legacy rows or non-voucher intents.
        /// </summary>
        [Column("payload_secrets_protected")]
        public string? PayloadSecretsProtected { get; set; }

        /// <summary>
        /// Normalized payload hash (SHA-256) to deduplicate identical intents across offline IDs.
        /// Unique constraint is enforced on (CashRegisterId, PayloadHash) for non-null values.
        /// </summary>
        [MaxLength(64)]
        [Column("payload_hash")]
        public string? PayloadHash { get; set; }

        /// <summary>
        /// Server UTC timestamp when this offline intent was first received/persisted.
        /// </summary>
        [Required]
        [Column("server_received_at_utc")]
        public DateTime ServerReceivedAtUtc { get; set; }

        /// <summary>
        /// Client-side UTC timestamp when the offline intent was created (device time).
        /// </summary>
        [Required]
        public DateTime OfflineCreatedAtUtc { get; set; }

        /// <summary>
        /// Optional device identifier for monotonic client sequence tracking.
        /// </summary>
        [MaxLength(128)]
        [Column("device_id")]
        public string? DeviceId { get; set; }

        /// <summary>
        /// Optional client sequence number for (DeviceId + CashRegisterId) monotonic validation.
        /// </summary>
        [Column("client_sequence_number")]
        public int? ClientSequenceNumber { get; set; }

        [Required]
        [Column("clock_drift_warning")]
        public bool ClockDriftWarning { get; set; } = false;

        [Required]
        [Column("sequence_gap_detected")]
        public bool SequenceGapDetected { get; set; } = false;

        [Required]
        [Column("sequence_duplicate_detected")]
        public bool SequenceDuplicateDetected { get; set; } = false;

        [Required]
        [MaxLength(20)]
        public OfflineTransactionStatus Status { get; set; } = OfflineTransactionStatus.Pending;

        /// <summary>
        /// Set only after successful replay (synced to a real fiscal payment).
        /// </summary>
        public Guid? SyncedPaymentId { get; set; }

        /// <summary>
        /// UTC when replay completed and fiscal payment/receipt was created (server time).
        /// </summary>
        public DateTime? FiscalizedAtUtc { get; set; }

        [MaxLength(64)]
        public string? LastErrorCode { get; set; }

        [MaxLength(512)]
        public string? LastErrorMessageSafe { get; set; }

        public int RetryCount { get; set; }

        public DateTime? LastReplayAttemptAt { get; set; }
    }

    public enum OfflineTransactionStatus
    {
        Pending = 0,
        Synced = 1,
        Failed = 2,
        /// <summary>Server-accepted non-fiscal intent when TSE is offline (cash/card only); replayed by the offline replay worker.</summary>
        NonFiscalPending = 3
    }
}

