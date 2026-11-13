using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

public enum PaymentReversalOperation
{
    Cancel = 1,
    Refund = 2
}

public enum PaymentReversalApprovalStatus
{
    Pending = 1,
    Consumed = 2,
    Expired = 3
}

[Table("payment_reversal_approvals")]
public class PaymentReversalApproval : BaseEntity, ITenantEntity
{
    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [Column("payment_id")]
    public Guid PaymentId { get; set; }

    [Required]
    [Column("operation")]
    public PaymentReversalOperation Operation { get; set; }

    [Column("refund_amount", TypeName = "decimal(18,2)")]
    public decimal? RefundAmount { get; set; }

    [Required]
    [Column("reason", TypeName = "text")]
    public string Reason { get; set; } = string.Empty;

    [Column("reason_code")]
    public int ReasonCode { get; set; }

    [Required]
    [Column("status")]
    public PaymentReversalApprovalStatus Status { get; set; } = PaymentReversalApprovalStatus.Pending;

    [Column("approval_token_hash")]
    [MaxLength(200)]
    public string? ApprovalTokenHash { get; set; }

    [Column("approval_token_expires_at_utc")]
    public DateTime? ApprovalTokenExpiresAtUtc { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("requested_by_user_id")]
    public string RequestedByUserId { get; set; } = string.Empty;

    [Column("consumed_at_utc")]
    public DateTime? ConsumedAtUtc { get; set; }

    [MaxLength(64)]
    [Column("idempotency_key")]
    public string? IdempotencyKey { get; set; }
}
