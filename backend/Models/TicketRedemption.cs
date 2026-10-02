using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

public enum TicketRedemptionStatus
{
    Valid = 0,
    Redeemed = 1,
    Cancelled = 2,
    Expired = 3,
}

[Table("ticket_redemptions")]
public sealed class TicketRedemption : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Column("payment_detail_id")]
    public Guid? PaymentDetailId { get; set; }

    public PaymentDetails? PaymentDetail { get; set; }

    /// <summary>
    /// Non-secret display fragment (hash prefix). Never the issued plaintext code.
    /// </summary>
    [Required]
    [MaxLength(64)]
    [Column("ticket_code")]
    public string TicketCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("ticket_code_hash")]
    public string TicketCodeHash { get; set; } = string.Empty;

    [Column("status")]
    public TicketRedemptionStatus Status { get; set; } = TicketRedemptionStatus.Valid;

    [Column("valid_from_utc")]
    public DateTime? ValidFromUtc { get; set; }

    [Column("valid_until_utc")]
    public DateTime? ValidUntilUtc { get; set; }

    [Column("redeemed_at_utc")]
    public DateTime? RedeemedAtUtc { get; set; }

    [MaxLength(450)]
    [Column("redeemed_by_user_id")]
    public string? RedeemedByUserId { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
