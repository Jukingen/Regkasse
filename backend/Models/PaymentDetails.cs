using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace KasseAPI_Final.Models
{
    [Table("payment_details")]
    public class PaymentDetails : BaseEntity
    {
        [Required]
        public Guid CustomerId { get; set; }

        [Required]
        [MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        // Yeni eklenen alanlar - Frontend PaymentModal'dan gelen
        [Required]
        public int TableNumber { get; set; } // Masa numarası

        [Required]
        [MaxLength(100)]
        public string CashierId { get; set; } = string.Empty; // Kasiyer ID

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        // DB column "PaymentMethod" is varchar storing numeric strings like '0', '1', etc.
        [Required]
        [MaxLength(50)]
        [Column("PaymentMethod")]
        public string PaymentMethodRaw { get; set; } = "0";

        // Enum helper for type-safe access (not mapped to DB)
        [NotMapped]
        public PaymentMethod PaymentMethod
        {
            get
            {
                if (int.TryParse(PaymentMethodRaw, out int value) && Enum.IsDefined(typeof(PaymentMethod), value))
                    return (PaymentMethod)value;
                return PaymentMethod.Cash; // Default fallback
            }
            set
            {
                PaymentMethodRaw = ((int)value).ToString();
            }
        }

        // Avusturya yasal gereksinimleri (RKSV & DSGVO)
        [Required]
        [MaxLength(12)]
        [RegularExpression(@"^ATU\d{8}$", ErrorMessage = "Steuernummer formatı ATU12345678 olmalıdır")]
        public string Steuernummer { get; set; } = string.Empty; // Vergi numarası (ATU12345678)

        /// <summary>RKSV §8 snapshot: Unternehmensbezeichnung at payment time (from <see cref="CompanySettings"/>).</summary>
        [MaxLength(100)]
        public string? CompanyName { get; set; }

        /// <summary>RKSV §8 snapshot: Sitz der gewerblichen Betriebsstätte at payment time (from <see cref="CompanySettings"/>).</summary>
        [MaxLength(200)]
        public string? CompanyAddress { get; set; }

        /// <summary>FK to cash_registers. Required; no Guid.Empty. Fiscal display id (Kassen-ID) comes from CashRegister.RegisterNumber.</summary>
        [Required]
        [Column("cash_register_id")]
        public Guid CashRegisterId { get; set; }

        [ForeignKey(nameof(CashRegisterId))]
        public virtual CashRegister? CashRegister { get; set; }

        [Column(TypeName = "text")]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string? TransactionId { get; set; }

        [Required]
        [Column(TypeName = "text")]
        public string TseSignature { get; set; } = string.Empty; // RKSV §6 COMPACT JWS

        /// <summary>SHA-1 thumbprint of the TSE signing certificate used for <see cref="TseSignature"/> (DEP grouping).</summary>
        [MaxLength(64)]
        [Column("certificate_thumbprint")]
        public string? CertificateThumbprint { get; set; }

        [Column(TypeName = "text")]
        public string? PrevSignatureValueUsed { get; set; } // Imza zinciri için önceki signature

        [Required]
        public DateTime TseTimestamp { get; set; } // TSE zaman damgası

        // --- Audit/Fiscal metadata (optional, backward compatible) ---
        // Added for better traceability of refund/cancel operations.
        public Guid? OriginalPaymentId { get; set; }
        public bool IsRefund { get; set; } = false;
        /// <summary>True when this row is a storno (cancellation reversal). Original payment is never modified; this reversal references it via OriginalPaymentId.</summary>
        public bool IsStorno { get; set; } = false;

        /// <summary>RKSV Storno classification when <see cref="IsStorno"/> is true; null for legacy or non-storno rows.</summary>
        public StornoReason? StornoReason { get; set; }

        /// <summary>For storno/refund rows: canonical receipt id of the original sale (forensic link).</summary>
        public Guid? OriginalReceiptId { get; set; }

        /// <summary>
        /// When the payment was created from a controlled offline intent,
        /// this links the canonical fiscal PaymentDetails to the original OfflineTransaction.
        /// </summary>
        public Guid? OfflineTransactionId { get; set; }
        public virtual OfflineTransaction? OfflineTransaction { get; set; }

        /// <summary>
        /// Server-generated id for one POST /replay batch; links payment/receipt audits to the same offline replay operation.
        /// </summary>
        [Column("offline_replay_batch_correlation_id")]
        public Guid? OfflineReplayBatchCorrelationId { get; set; }
        [MaxLength(200)]
        public string? RefundReason { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal? RefundAmount { get; set; }
        public DateTime? RefundedAt { get; set; }
        [Column(TypeName = "text")]
        public string? CancellationReason { get; set; }
        public DateTime? CancelledAt { get; set; }

        // RKSV verification normalization (Phase 1) - nullable, eski kayıtlarla uyumlu
        [MaxLength(50)]
        [Column("signature_format")]
        public string? SignatureFormat { get; set; }

        [Column("jws_header")]
        public string? JwsHeader { get; set; }

        [Column("jws_payload")]
        public string? JwsPayload { get; set; }

        [Column("jws_signature")]
        public string? JwsSignature { get; set; }

        [MaxLength(50)]
        public string? Provider { get; set; }

        [MaxLength(100)]
        [Column("correlation_id")]
        public string? CorrelationId { get; set; }

        // Tax details as JSONB (PostgreSQL)
        [Column(TypeName = "jsonb")]
        public JsonDocument TaxDetails { get; set; } = JsonDocument.Parse("{}");

        // Payment items as JSONB
        [Column(TypeName = "jsonb")]
        public JsonDocument PaymentItems { get; set; } = JsonDocument.Parse("[]");

        // Receipt/Invoice fields
        [Required]
        [Column(TypeName = "text")]
        public string ReceiptNumber { get; set; } = string.Empty; // Format: AT-{TSE_ID}-{YYYYMMDD}-{SEQ}

        public bool IsPrinted { get; set; } = false;

        /// <summary>Immutable snapshot of customer benefits applied at payment time (e.g. percentage discount, free allowance). Null when no benefits applied.</summary>
        [Column(TypeName = "jsonb")]
        public JsonDocument? AppliedBenefitsSnapshot { get; set; }

        /// <summary>Client-provided idempotency key for this payment attempt. Unique per key; used to avoid duplicate payments on retry.</summary>
        [MaxLength(64)]
        [Column("idempotency_key")]
        public string? IdempotencyKey { get; set; }

        /// <summary>Sprint 6: Client-provided idempotency key for cancel operation. When set, retries with same key return this cancelled payment.</summary>
        [MaxLength(64)]
        [Column("cancel_idempotency_key")]
        public string? CancelIdempotencyKey { get; set; }

        /// <summary>
        /// Derived FinanzOnline submit snapshot (post-payment enqueue/retry). NotSent / Pending / Submitted / Failed / NeedsReconciliation.
        /// Authoritative BMF submission lifecycle for invoices is <c>finanz_online_outbox_messages</c> (admin: GET /api/admin/finanzonline-outbox).
        /// </summary>
        [MaxLength(30)]
        [Column("finanz_online_status")]
        public string? FinanzOnlineStatus { get; set; }
        [Column("finanz_online_error", TypeName = "text")]
        public string? FinanzOnlineError { get; set; }
        [MaxLength(100)]
        [Column("finanz_online_reference_id")]
        public string? FinanzOnlineReferenceId { get; set; }
        [Column("finanz_online_last_attempt_at_utc")]
        public DateTime? FinanzOnlineLastAttemptAtUtc { get; set; }
        [Column("finanz_online_retry_count")]
        public int FinanzOnlineRetryCount { get; set; }

        /// <summary>RKSV Sonderbeleg; NULL = normal fiscal payment row.</summary>
        [MaxLength(20)]
        [Column("rksv_special_receipt_kind")]
        public string? RksvSpecialReceiptKind { get; set; }

        /// <summary>Vienna calendar year for Monats-Nullbeleg (duplicate guard).</summary>
        [Column("rksv_special_receipt_year")]
        public int? RksvSpecialReceiptYear { get; set; }

        /// <summary>Vienna calendar month 1–12 for Monatsbeleg / Nullbeleg; null for Jahresbeleg (year-only).</summary>
        [Column("rksv_special_receipt_month")]
        public int? RksvSpecialReceiptMonth { get; set; }

        /// <summary>December Nullbeleg may later be treated as Jahresbeleg; no extra business logic in phase 1.</summary>
        [Column("rksv_nullbeleg_acts_as_jahresbeleg")]
        public bool RksvNullbelegActsAsJahresbeleg { get; set; }

        /// <summary>
        /// True when a Monatsbeleg was created after the legal deadline for its target period had passed
        /// (nachträglich / verspätet). This does NOT backdate the receipt: <see cref="BaseEntity.CreatedAt"/>
        /// and <see cref="TseTimestamp"/> remain the real signing time. The covered period is carried by
        /// <see cref="RksvSpecialReceiptYear"/> / <see cref="RksvSpecialReceiptMonth"/>. Used for transparent
        /// compliance reporting during a Betriebsprüfung.
        /// </summary>
        [Column("rksv_is_late_created")]
        public bool IsLateCreated { get; set; }

        /// <summary>Operator-provided reason documenting why a late (nachträglich) Monatsbeleg was created; null for on-time receipts.</summary>
        [MaxLength(500)]
        [Column("rksv_late_creation_reason")]
        public string? LateCreationReason { get; set; }

        /// <summary>
        /// The month/year (period end) this Sonderbeleg covers — Vienna calendar anchor, not the signing time.
        /// Monatsbeleg: last day of <see cref="RksvSpecialReceiptYear"/>/<see cref="RksvSpecialReceiptMonth"/>.
        /// Jahresbeleg: December 31 of <see cref="RksvSpecialReceiptYear"/>.
        /// Does not affect <see cref="BaseEntity.CreatedAt"/> or TSE signing time.
        /// </summary>
        [Column("rksv_intended_period_date", TypeName = "date")]
        public DateTime? IntendedPeriodDate { get; set; }

        /// <summary>
        /// Set when this fiscal payment was accepted from an offline replay while NTP/system clock was outside RKSV tolerance.
        /// </summary>
        [Column("time_sync_warning")]
        public bool TimeSyncWarning { get; set; }

        // Navigation properties
        public virtual Customer? Customer { get; set; }
    }
}

