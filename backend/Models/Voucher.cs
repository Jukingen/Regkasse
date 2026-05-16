using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// POS Gutschein lifecycle state (persisted as int).
/// </summary>
public enum VoucherStatus
{
    Active = 0,
    PartiallyRedeemed = 1,
    Redeemed = 2,
    Cancelled = 3,
    Expired = 4
}

/// <summary>
/// Immutable ledger line for voucher balance changes (persisted as int).
/// </summary>
public enum VoucherTransactionType
{
    Issue = 0,
    Redeem = 1,
    Refund = 2,
    Cancel = 3,
    Expire = 4
}

/// <summary>
/// Tenant-scoped stored-value voucher. Raw codes are never persisted; lookup uses <see cref="CodeHash"/> only.
/// </summary>
[Table("vouchers")]
public class Voucher : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    /// <summary>Optional buyer/recipient customer (POS issuance).</summary>
    [Column("customer_id")]
    public Guid? CustomerId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public virtual Customer? Customer { get; set; }

    /// <summary>SHA-256 hex (or app-defined normalized hash) of the canonical voucher code; not reversible.</summary>
    [Required]
    [MaxLength(64)]
    [Column("code_hash")]
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>Operator-safe hint (e.g. last digits); must not reveal full code.</summary>
    [Required]
    [MaxLength(32)]
    [Column("masked_code")]
    public string MaskedCode { get; set; } = string.Empty;

    [Required]
    [Column("initial_amount", TypeName = "decimal(18,2)")]
    public decimal InitialAmount { get; set; }

    [Required]
    [Column("remaining_amount", TypeName = "decimal(18,2)")]
    public decimal RemainingAmount { get; set; }

    [Required]
    [MaxLength(3)]
    [Column("currency")]
    public string Currency { get; set; } = "EUR";

    [Required]
    [Column("status")]
    public VoucherStatus Status { get; set; } = VoucherStatus.Active;

    [Required]
    [Column("valid_from_utc")]
    public DateTime ValidFromUtc { get; set; }

    [Required]
    [Column("expires_at_utc")]
    public DateTime ExpiresAtUtc { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("created_by_user_id")]
    public string CreatedByUserId { get; set; } = string.Empty;

    [Required]
    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("cancelled_at_utc")]
    public DateTime? CancelledAtUtc { get; set; }

    [MaxLength(500)]
    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

    /// <summary>Optional operator note at issuance (admin-only, not printed on POS receipt).</summary>
    [MaxLength(500)]
    [Column("internal_note")]
    public string? InternalNote { get; set; }

    public virtual ICollection<VoucherLedgerEntry> LedgerEntries { get; set; } = new List<VoucherLedgerEntry>();
}

/// <summary>
/// Append-only audit trail for voucher balance movements. <see cref="Amount"/> is the signed delta; <see cref="BalanceAfter"/> is authoritative snapshot.
/// </summary>
[Table("voucher_ledger_entries")]
public class VoucherLedgerEntry : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [Required]
    [Column("voucher_id")]
    public Guid VoucherId { get; set; }

    [ForeignKey(nameof(VoucherId))]
    public virtual Voucher? Voucher { get; set; }

    [Column("payment_id")]
    public Guid? PaymentId { get; set; }

    [ForeignKey(nameof(PaymentId))]
    public virtual PaymentDetails? Payment { get; set; }

    [Column("receipt_id")]
    public Guid? ReceiptId { get; set; }

    [ForeignKey(nameof(ReceiptId))]
    public virtual Receipt? Receipt { get; set; }

    [Required]
    [Column("type")]
    public VoucherTransactionType Type { get; set; }

    /// <summary>Signed change applied to <see cref="Voucher.RemainingAmount"/> (e.g. Issue +100, Redeem -25).</summary>
    [Required]
    [Column("amount", TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [Column("balance_after", TypeName = "decimal(18,2)")]
    public decimal BalanceAfter { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("created_by_user_id")]
    public string CreatedByUserId { get; set; } = string.Empty;

    [Required]
    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [MaxLength(100)]
    [Column("correlation_id")]
    public string? CorrelationId { get; set; }

    [MaxLength(128)]
    [Column("idempotency_key")]
    public string? IdempotencyKey { get; set; }
}
