using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Super Admin SaaS billing audit trail. Platform-scoped — not written to fiscal <see cref="AuditLog"/>.
/// </summary>
[Table("billing_audit_log")]
public class BillingAuditLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid? TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    /// <summary>SALE_CREATED, SALE_CANCELLED, etc. — see <see cref="BillingAuditEventTypes"/>.</summary>
    [Column("action")]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    [Column("sale_id")]
    public Guid? SaleId { get; set; }

    [ForeignKey(nameof(SaleId))]
    public virtual LicenseSale? Sale { get; set; }

    [Column("details", TypeName = "jsonb")]
    public string? Details { get; set; }

    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [Column("timestamp_utc")]
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
