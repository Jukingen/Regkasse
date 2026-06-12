using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KasseAPI_Final.Converters;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs
{
    /// <summary>
    /// POS payment create request. Authoritative inputs: <see cref="CustomerId"/>, <see cref="Items"/>, <see cref="Payment"/>, <see cref="CashRegisterId"/> (register FK).
    /// <see cref="TotalAmount"/> is a client gross hint; the server recomputes totals from catalog pricing and rejects on mismatch (not authoritative).
    /// <see cref="Steuernummer"/> is optional; when empty or invalid, the server substitutes from company profile — do not treat the raw request field as sole fiscal authority.
    /// When <see cref="IsStorno"/> or <see cref="IsRefund"/> is set, this request performs a fiscal reversal linked by <see cref="OriginalReceiptNumber"/> (RKSV: Storno vs partial refund).
    /// </summary>
    public class CreatePaymentRequest : IValidatableObject
    {
        [Required]
        public Guid CustomerId { get; set; }

        /// <summary>Required for normal sales; may be empty when <see cref="IsStorno"/> or <see cref="IsRefund"/> is true.</summary>
        public List<PaymentItemRequest> Items { get; set; } = new();

        [Required]
        public PaymentMethodRequest Payment { get; set; } = new();

        [Required]
        public int TableNumber { get; set; }

        /// <summary>
        /// Normal sale: client gross hint for parity with server catalog totals.
        /// Refund via create: positive refund gross amount.
        /// Storno via create: must match original receipt gross total (parity).
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>Optional UID hint (ATU########). Normalized from company profile when omitted or invalid after validation rules.</summary>
        [RegularExpression(@"^ATU\d{8}$", ErrorMessage = "Steuernummer must be in format ATU12345678")]
        public string? Steuernummer { get; set; }

        /// <summary>Required: POS cash register row (FK). Must not be empty GUID.</summary>
        [Required]
        public Guid CashRegisterId { get; set; }

        public string? Notes { get; set; }

        /// <summary>Optional idempotency key for this payment attempt. When retried with the same key, the existing payment result is returned.</summary>
        [MaxLength(64)]
        public string? IdempotencyKey { get; set; }

        /// <summary>Optional explicit customer classification; when omitted, derived from customer id (e.g. walk-in sentinel).</summary>
        public CustomerKind? CustomerKind { get; set; }

        /// <summary>True: full receipt cancellation (Storno); mutually exclusive with <see cref="IsRefund"/>.</summary>
        public bool IsStorno { get; set; }

        /// <summary>True: partial return (Refund); mutually exclusive with <see cref="IsStorno"/>.</summary>
        public bool IsRefund { get; set; }

        /// <summary>Original sale BelegNr / receipt number; required when <see cref="IsStorno"/> or <see cref="IsRefund"/>.</summary>
        [MaxLength(256)]
        public string? OriginalReceiptNumber { get; set; }

        /// <summary>RKSV Storno reason; required when <see cref="IsStorno"/>.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StornoReason? StornoReason { get; set; }

        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IsStorno && IsRefund)
            {
                yield return new ValidationResult(
                    "IsStorno and IsRefund cannot both be true.",
                    [nameof(IsStorno), nameof(IsRefund)]);
            }

            if (IsStorno || IsRefund)
            {
                if (string.IsNullOrWhiteSpace(OriginalReceiptNumber))
                {
                    yield return new ValidationResult(
                        "OriginalReceiptNumber is required for storno or refund.",
                        [nameof(OriginalReceiptNumber)]);
                }

                if (IsStorno && !StornoReason.HasValue)
                {
                    yield return new ValidationResult(
                        "StornoReason is required when IsStorno is true.",
                        [nameof(StornoReason)]);
                }

                if (IsStorno && StornoReason == global::KasseAPI_Final.Models.StornoReason.None)
                {
                    yield return new ValidationResult(
                        "StornoReason must be a concrete RKSV value (not None).",
                        [nameof(StornoReason)]);
                }
            }

            if (!IsStorno && !IsRefund)
            {
                if (Items == null || Items.Count == 0)
                    yield return new ValidationResult("At least one payment line item is required.", [nameof(Items)]);
                if (TotalAmount < 0.01m)
                    yield return new ValidationResult("Total amount must be greater than 0.", [nameof(TotalAmount)]);
            }
            else if (IsRefund)
            {
                if (TotalAmount < 0.01m)
                    yield return new ValidationResult("Refund amount (TotalAmount) must be greater than zero.", [nameof(TotalAmount)]);
            }
            else if (IsStorno)
            {
                if (TotalAmount < 0.01m)
                    yield return new ValidationResult("TotalAmount must be greater than zero for storno parity.", [nameof(TotalAmount)]);
            }
        }
    }
    
    /// <summary>
    /// Phase 2 deprecated: Satır bazlı extra/modifier. Yeni akış: add-on = ayrı PaymentItem (productId). Legacy compat için hâlâ kabul edilir.
    /// </summary>
    public class PaymentItemModifierRequest
    {
        [Required]
        public Guid ModifierId { get; set; }

        /// <summary>Miktar (varsayılan 1).</summary>
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;

        /// <summary>Opsiyonel: FE gönderirse DB fiyatı ile karşılaştırılır; uyuşmazsa 400. Hesaplama her zaman DB fiyatı ile yapılır. 2 dp.</summary>
        [Range(0, double.MaxValue, ErrorMessage = "PriceDelta must be >= 0 when provided")]
        public decimal? PriceDelta { get; set; }

        /// <summary>Opsiyonel modifier grup ID (katalog ilişkisi doğrulaması için).</summary>
        public Guid? GroupId { get; set; }
    }

    /// <summary>
    /// Ödeme kalemi request'i. Primary: productId, quantity, taxType (one line per product). Legacy: ModifierIds/Modifiers for old carts only.
    /// </summary>
    public class PaymentItemRequest
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public int Quantity { get; set; }

        /// <summary>Vergi tipi. standard, reduced, special, zerorate.</summary>
        [Required]
        [JsonConverter(typeof(TaxTypeJsonConverter))]
        public TaxType TaxType { get; set; } = TaxType.Standard;

        /// <summary>Phase 2 legacy: Add-ons should be separate PaymentItem lines. Kept for read/write compatibility with old clients.</summary>
        [Obsolete("Add-ons as separate items (productId). Kept for backward compat.", false)]
        public List<Guid> ModifierIds { get; set; } = new();

        /// <summary>Phase 2 legacy: Add-ons as separate items. Kept for backward compat.</summary>
        [Obsolete("Add-ons as separate items. Kept for backward compat.", false)]
        public List<PaymentItemModifierRequest>? Modifiers { get; set; }
    }
    
    /// <summary>
    /// Ödeme yöntemi request'i
    /// </summary>
    public class PaymentMethodRequest
    {
        [Required]
        public string Method { get; set; } = "cash"; // cash, card, voucher
        
        [Required]
        public bool TseRequired { get; set; } = true;
        
        public decimal? Amount { get; set; }

        /// <summary>Plaintext voucher code for single-voucher payment (never stored on <see cref="PaymentDetails"/>).</summary>
        [MaxLength(128)]
        public string? VoucherCode { get; set; }

        /// <summary>Optional multi-voucher split; amounts must sum to the fiscal sale total when method is voucher.</summary>
        public List<VoucherRedemptionRequestItem>? VoucherRedemptions { get; set; }

        /// <summary>Confirmed card payment intent id when method is card (Stripe-ready two-step flow).</summary>
        public Guid? CardPaymentIntentId { get; set; }
    }
    
    /// <summary>
    /// Ödeme sonucu
    /// </summary>
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public PaymentDetails? Payment { get; set; }
        public Guid? PaymentId { get; set; }
        public string? TseSignature { get; set; }
        /// <summary>RKSV belegdaten veya NON_FISCAL_DEMO formatında QR payload.</summary>
        public string? QrPayload { get; set; }
        /// <summary>true: tseRequired=false veya demo/soft TSE ile imzalandı.</summary>
        public bool IsDemoFiscal { get; set; }
        /// <summary>TSE provider: "Hardware", "Software", "Demo".</summary>
        public string? TseProvider { get; set; }
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Non-sensitive diagnostic code when Success is false (e.g. DEMO_BY_FLAG, DEMO_BY_ROLE). For ops/debug only.
        /// </summary>
        public string? DiagnosticCode { get; set; }

        /// <summary>When true (default), invoice was persisted for this payment. When false, payment succeeded but invoice sync failed — operator attention required.</summary>
        public bool InvoicePersisted { get; set; } = true;

        /// <summary>True when the same idempotency key returned an existing committed payment (no new fiscal write).</summary>
        public bool IdempotentReplay { get; set; }

        /// <summary>True if failure is deterministic (e.g. invalid register, missing customer). Replays should not retry.</summary>
        public bool IsDeterministicFailure { get; set; }

        /// <summary>
        /// True when payment was accepted as a server-side offline intent (NonFiscalPending); fiscal receipt is produced later via replay.
        /// </summary>
        public bool NonFiscalOfflineQueued { get; set; }

        /// <summary>Offline intent id when <see cref="NonFiscalOfflineQueued"/> is true.</summary>
        public Guid? OfflineTransactionId { get; set; }

        /// <summary>High-risk reversal requires manager approval before execution.</summary>
        public bool RequiresApproval { get; set; }

        public Guid? ApprovalRequestId { get; set; }

        public DateTime? ApprovalTokenExpiresAtUtc { get; set; }

        public bool ApprovalNotificationSent { get; set; }

        /// <summary>True when the payment was stored while NTP clock drift was outside tolerance (typically offline replay only).</summary>
        public bool TimeSyncWarning { get; set; }
    }
    
    /// <summary>
    /// Ödeme istatistikleri
    /// </summary>
    public class PaymentStatistics
    {
        public int TotalPayments { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Dictionary<string, int> PaymentsByMethod { get; set; } = new();
        public Dictionary<string, decimal> AmountByMethod { get; set; } = new();
        public Dictionary<string, int> PaymentsByTaxType { get; set; } = new();
        
        // TSE ve FinanzOnline istatistikleri
        public int TseSignedPayments { get; set; }
        public decimal TseSignedAmount { get; set; }
        public int FinanzOnlineSentPayments { get; set; }
        public decimal FinanzOnlineSentAmount { get; set; }
    }
}
