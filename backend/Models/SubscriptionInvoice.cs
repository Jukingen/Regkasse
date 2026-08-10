using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KasseAPI_Final.Models.Enums;

namespace KasseAPI_Final.Models;

/// <summary>SaaS subscription invoice (non-fiscal). Distinct from POS/RKSV <see cref="Invoice"/>.</summary>
[Table("subscription_invoices")]
public class SubscriptionInvoice
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [Required]
    [MaxLength(40)]
    [Column("invoice_number")]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Billing period start (UTC, inclusive).</summary>
    [Column("period_start_utc")]
    public DateTime PeriodStartUtc { get; set; }

    /// <summary>Billing period end (UTC, exclusive).</summary>
    [Column("period_end_utc")]
    public DateTime PeriodEndUtc { get; set; }

    [Column("license_type")]
    public LicenseType LicenseType { get; set; }

    [Column("amount_net", TypeName = "numeric(18,2)")]
    public decimal AmountNet { get; set; }

    [Column("vat_rate", TypeName = "numeric(5,2)")]
    public decimal VatRate { get; set; } = 20m;

    [Column("amount_vat", TypeName = "numeric(18,2)")]
    public decimal AmountVat { get; set; }

    [Column("amount_gross", TypeName = "numeric(18,2)")]
    public decimal AmountGross { get; set; }

    [Required]
    [MaxLength(3)]
    [Column("currency")]
    public string Currency { get; set; } = "EUR";

    /// <summary>draft | issued | paid | void</summary>
    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = SubscriptionInvoiceStatuses.Issued;

    [MaxLength(500)]
    [Column("pdf_path")]
    public string? PdfPath { get; set; }

    [Column("issued_at_utc")]
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class SubscriptionInvoiceStatuses
{
    public const string Draft = "draft";
    public const string Issued = "issued";
    public const string Paid = "paid";
    public const string Void = "void";
}
