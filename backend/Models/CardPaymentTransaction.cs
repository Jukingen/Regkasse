using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KasseAPI_Final.Services.PaymentGateway;

namespace KasseAPI_Final.Models;

/// <summary>
/// Card acquirer transaction (Mock or Stripe). Intent rows may exist before fiscal <see cref="PaymentDetails"/> commit;
/// <see cref="PaymentId"/> is set when linked to the canonical payment row.
/// </summary>
[Table("card_payment_transactions")]
public class CardPaymentTransaction : BaseTenantEntity
{
    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    /// <summary>FK to payment_details; null until fiscal payment is committed.</summary>
    [Column("payment_id")]
    public Guid? PaymentId { get; set; }

    [ForeignKey(nameof(PaymentId))]
    public virtual PaymentDetails? Payment { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("gateway")]
    public string Gateway { get; set; } = "Mock";

    [MaxLength(100)]
    [Column("gateway_transaction_id")]
    public string? GatewayTransactionId { get; set; }

    [MaxLength(128)]
    [Column("gateway_payment_intent_id")]
    public string? GatewayPaymentIntentId { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "EUR";

    [MaxLength(4)]
    [Column("card_last4")]
    public string? CardLast4 { get; set; }

    [MaxLength(20)]
    [Column("card_brand")]
    public string? CardBrand { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = CardPaymentTransactionStatuses.Created;

    [Column("error_message", TypeName = "text")]
    public string? ErrorMessage { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    // Intent lifecycle / admin query helpers (not in minimal spec DTO, required by POS two-step flow)
    [Required]
    [Column("cash_register_id")]
    public Guid CashRegisterId { get; set; }

    [MaxLength(128)]
    [Column("client_secret")]
    public string? ClientSecret { get; set; }

    [Column("refunded_at_utc")]
    public DateTime? RefundedAtUtc { get; set; }

    [Column("refunded_amount", TypeName = "decimal(18,2)")]
    public decimal? RefundedAmount { get; set; }

    [MaxLength(450)]
    [Column("created_by_user_id")]
    public string? CreatedByUserId { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Column("metadata_json", TypeName = "jsonb")]
    public string MetadataJson { get; set; } = "{}";
}

public static class CardPaymentTransactionStatuses
{
    public const string Created = "Created";
    public const string Pending = "Pending";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
    public const string Refunded = "Refunded";

    public static PaymentIntentStatus ToPaymentIntentStatus(string status) =>
        status switch
        {
            Created => PaymentIntentStatus.Created,
            Pending => PaymentIntentStatus.Pending,
            Succeeded => PaymentIntentStatus.Succeeded,
            Failed => PaymentIntentStatus.Failed,
            Cancelled => PaymentIntentStatus.Cancelled,
            Refunded => PaymentIntentStatus.Refunded,
            _ => PaymentIntentStatus.Created
        };

    public static string FromPaymentIntentStatus(PaymentIntentStatus status) =>
        status switch
        {
            PaymentIntentStatus.Created => Created,
            PaymentIntentStatus.Pending => Pending,
            PaymentIntentStatus.Succeeded => Succeeded,
            PaymentIntentStatus.Failed => Failed,
            PaymentIntentStatus.Cancelled => Cancelled,
            PaymentIntentStatus.Refunded => Refunded,
            _ => Created
        };
}
