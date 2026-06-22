using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Super Admin SaaS billing audit trail. Platform-scoped — not written to fiscal <see cref="AuditLog"/>.
/// </summary>
[Table("billing_audit_log")]
public sealed class BillingAuditLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("license_sale_id")]
    public Guid LicenseSaleId { get; set; }

    [ForeignKey(nameof(LicenseSaleId))]
    public LicenseSale? LicenseSale { get; set; }

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    /// <summary><see cref="BillingAuditEventTypes"/> value.</summary>
    [Required]
    [Column("event_type")]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    [Column("actor_user_id")]
    [MaxLength(450)]
    public string ActorUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(ActorUserId))]
    public ApplicationUser? ActorUser { get; set; }

    [Required]
    [Column("price_net", TypeName = "decimal(10,2)")]
    public decimal PriceNet { get; set; }

    [Required]
    [Column("price_gross", TypeName = "decimal(10,2)")]
    public decimal PriceGross { get; set; }

    [Required]
    [Column("currency")]
    [MaxLength(3)]
    public string Currency { get; set; } = "EUR";

    [Column("invoice_number")]
    [MaxLength(50)]
    public string? InvoiceNumber { get; set; }

    [Column("license_key")]
    [MaxLength(100)]
    public string? LicenseKey { get; set; }

    [Column("license_plan")]
    [MaxLength(50)]
    public string? LicensePlan { get; set; }

    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

    [Required]
    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
