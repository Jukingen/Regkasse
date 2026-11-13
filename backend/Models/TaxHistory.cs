using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Append-only journal of product VAT / tax-group rate changes (Austrian MwSt compliance trail).
/// </summary>
[Table("tax_history")]
public class TaxHistory : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    /// <summary>Tax group after the change.</summary>
    [Column("tax_group_id")]
    public Guid TaxGroupId { get; set; }

    [Column("old_rate", TypeName = "decimal(5,2)")]
    public decimal OldRate { get; set; }

    [Column("new_rate", TypeName = "decimal(5,2)")]
    public decimal NewRate { get; set; }

    [Column("changed_at")]
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Actor user id (Identity id as Guid when parseable).</summary>
    [Column("changed_by")]
    public Guid ChangedBy { get; set; }

    [MaxLength(500)]
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Optional fiscal document reference related to the change.</summary>
    [MaxLength(100)]
    [Column("invoice_number")]
    public string? InvoiceNumber { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [ForeignKey(nameof(ProductId))]
    public virtual Product? Product { get; set; }

    [ForeignKey(nameof(TaxGroupId))]
    public virtual TaxGroup? TaxGroup { get; set; }
}
