using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// RKSV Phase 2 monthly closing snapshot: aggregates daily closings with payment/tax breakdown and TSE chain.
/// Distinct from the zero-value RKSV Sonderbeleg row in <see cref="PaymentDetails"/>.
/// </summary>
[Table("monatsbeleg")]
public class Monatsbeleg : ITenantEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [Required]
    [Column("cash_register_id")]
    public Guid CashRegisterId { get; set; }

    [ForeignKey(nameof(CashRegisterId))]
    public virtual CashRegister? CashRegister { get; set; }

    public int Year { get; set; }

    /// <summary>Vienna calendar month 1–12.</summary>
    public int Month { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCash { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCard { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalVoucher { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalOther { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalGross { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalTax { get; set; }

    /// <summary>Tax amount at 20% standard rate bucket.</summary>
    [Column("tax_rate_20", TypeName = "decimal(18,2)")]
    public decimal TaxRate20 { get; set; }

    /// <summary>Tax amount at 10% reduced rate bucket.</summary>
    [Column("tax_rate_10", TypeName = "decimal(18,2)")]
    public decimal TaxRate10 { get; set; }

    /// <summary>Gross at 0% bucket.</summary>
    [Column("tax_rate_0", TypeName = "decimal(18,2)")]
    public decimal TaxRate0 { get; set; }

    public int TransactionCount { get; set; }

    /// <summary>Completed daily closings included in this monthly snapshot.</summary>
    public int DailyClosingCount { get; set; }

    [Column(TypeName = "text")]
    public string? TseSignature { get; set; }

    [MaxLength(50)]
    [Column("tse_signature_timestamp")]
    public string? TseSignatureTimestamp { get; set; }

    [MaxLength(64)]
    [Column("tse_certificate_thumbprint")]
    public string? TseCertificateThumbprint { get; set; }

    [Column(TypeName = "text")]
    public string? PreviousSignature { get; set; }

    public int SignatureChainLength { get; set; }

    public bool IsSimulated { get; set; }

    [Required]
    [MaxLength(20)]
    public string Environment { get; set; } = "Demo";

    [Required]
    [MaxLength(450)]
    [Column("created_by_user_id")]
    public string CreatedByUserId { get; set; } = string.Empty;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Optional link to legacy <see cref="DailyClosing"/> row (ClosingType = Monthly).</summary>
    [Column("daily_closing_id")]
    public Guid? DailyClosingId { get; set; }

    [ForeignKey(nameof(DailyClosingId))]
    public virtual DailyClosing? DailyClosing { get; set; }
}
