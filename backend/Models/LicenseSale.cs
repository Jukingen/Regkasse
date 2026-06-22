using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Super Admin SaaS billing record for a Mandanten license sale.
/// Isolated from RKSV / deployment (<see cref="IssuedLicense"/>) licensing.
/// </summary>
[Table("license_sales")]
public class LicenseSale : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant Tenant { get; set; } = null!;

    [Column("license_key")]
    [MaxLength(100)]
    public string LicenseKey { get; set; } = string.Empty;

    /// <summary>'6_months', '12_months', 'custom' — see <see cref="LicenseSalePlans"/>.</summary>
    [Column("license_plan")]
    [MaxLength(50)]
    public string LicensePlan { get; set; } = string.Empty;

    [Column("custom_valid_until_utc")]
    public DateTime? CustomValidUntilUtc { get; set; }

    [Column("valid_from_utc")]
    public DateTime ValidFromUtc { get; set; }

    [Column("valid_until_utc")]
    public DateTime ValidUntilUtc { get; set; }

    [Column("price_net", TypeName = "decimal(10,2)")]
    public decimal PriceNet { get; set; }

    [Column("vat_rate", TypeName = "decimal(5,2)")]
    public decimal VatRate { get; set; } = 20.00m;

    [Column("vat_amount", TypeName = "decimal(10,2)")]
    public decimal VatAmount { get; set; }

    [Column("price_gross", TypeName = "decimal(10,2)")]
    public decimal PriceGross { get; set; }

    [Column("currency")]
    [MaxLength(3)]
    public string Currency { get; set; } = "EUR";

    [Column("sold_at_utc")]
    public DateTime SoldAtUtc { get; set; } = DateTime.UtcNow;

    [Column("sold_by_user_id")]
    public Guid SoldByUserId { get; set; }

    [Column("invoice_number")]
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Column("invoice_pdf_path")]
    [MaxLength(500)]
    public string? InvoicePdfPath { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    /// <summary>active, cancelled, refunded — see <see cref="LicenseSaleStatuses"/>.</summary>
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = LicenseSaleStatuses.Active;

    [Column("cancelled_at_utc")]
    public DateTime? CancelledAtUtc { get; set; }

    [Column("cancelled_by_user_id")]
    public Guid? CancelledByUserId { get; set; }

    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

    [Column("activation_date_utc")]
    public DateTime? ActivationDateUtc { get; set; }

    [Column("last_extended_at_utc")]
    public DateTime? LastExtendedAtUtc { get; set; }

    [Column("extended_by_user_id")]
    public Guid? ExtendedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
