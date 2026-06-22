using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Super Admin SaaS billing record for a Mandanten license sale.
/// Platform-scoped (not <see cref="ITenantEntity"/>); references <see cref="Tenant"/> for reporting only.
/// Isolated from RKSV / deployment (<see cref="IssuedLicense"/>) licensing.
/// </summary>
[Table("license_sales")]
public sealed class LicenseSale
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    [Required]
    [Column("license_key")]
    [MaxLength(100)]
    public string LicenseKey { get; set; } = string.Empty;

    /// <summary><see cref="LicenseSalePlans"/> value.</summary>
    [Required]
    [Column("license_plan")]
    [MaxLength(50)]
    public string LicensePlan { get; set; } = string.Empty;

    /// <summary>Set when <see cref="LicensePlan"/> is <see cref="LicenseSalePlans.Custom"/>.</summary>
    [Column("custom_valid_until_utc")]
    public DateTime? CustomValidUntilUtc { get; set; }

    [Required]
    [Column("valid_from_utc")]
    public DateTime ValidFromUtc { get; set; }

    [Required]
    [Column("valid_until_utc")]
    public DateTime ValidUntilUtc { get; set; }

    [Required]
    [Column("price_net", TypeName = "decimal(10,2)")]
    public decimal PriceNet { get; set; }

    [Required]
    [Column("vat_rate", TypeName = "decimal(5,2)")]
    public decimal VatRate { get; set; } = 20.00m;

    [Required]
    [Column("vat_amount", TypeName = "decimal(10,2)")]
    public decimal VatAmount { get; set; }

    [Required]
    [Column("price_gross", TypeName = "decimal(10,2)")]
    public decimal PriceGross { get; set; }

    [Required]
    [Column("currency")]
    [MaxLength(3)]
    public string Currency { get; set; } = "EUR";

    [Required]
    [Column("sold_at_utc")]
    public DateTime SoldAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Identity user id of the Super Admin who recorded the sale.</summary>
    [Required]
    [Column("sold_by_user_id")]
    [MaxLength(450)]
    public string SoldByUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(SoldByUserId))]
    public ApplicationUser? SoldByUser { get; set; }

    [Required]
    [Column("invoice_number")]
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Column("invoice_pdf_path")]
    [MaxLength(500)]
    public string? InvoicePdfPath { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    /// <summary><see cref="LicenseSaleStatuses"/> value.</summary>
    [Required]
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = LicenseSaleStatuses.Active;

    [Column("cancelled_at_utc")]
    public DateTime? CancelledAtUtc { get; set; }

    [Column("cancelled_by_user_id")]
    [MaxLength(450)]
    public string? CancelledByUserId { get; set; }

    [ForeignKey(nameof(CancelledByUserId))]
    public ApplicationUser? CancelledByUser { get; set; }

    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
}
