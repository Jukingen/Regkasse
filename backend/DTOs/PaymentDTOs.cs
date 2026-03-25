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
    /// </summary>
    public class CreatePaymentRequest
    {
        [Required]
        public Guid CustomerId { get; set; }
        
        [Required]
        public List<PaymentItemRequest> Items { get; set; } = new();
        
        [Required]
        public PaymentMethodRequest Payment { get; set; } = new();
        
        [Required]
        public int TableNumber { get; set; }
        
        /// <summary>Client-reported gross total for parity check only; persisted totals come from server calculation.</summary>
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Total amount must be greater than 0")]
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
