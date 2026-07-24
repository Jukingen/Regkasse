using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Append-only journal of product price and tax-group changes (RKSV / MwSt compliance trail).
/// </summary>
[Table("product_price_history")]
public class ProductPriceHistory : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("old_price", TypeName = "decimal(18,2)")]
    public decimal OldPrice { get; set; }

    [Column("new_price", TypeName = "decimal(18,2)")]
    public decimal NewPrice { get; set; }

    [Column("old_tax_group_id")]
    public Guid OldTaxGroupId { get; set; }

    [Column("new_tax_group_id")]
    public Guid NewTaxGroupId { get; set; }

    [Column("old_tax_rate", TypeName = "decimal(5,2)")]
    public decimal OldTaxRate { get; set; }

    [Column("new_tax_rate", TypeName = "decimal(5,2)")]
    public decimal NewTaxRate { get; set; }

    [Column("effective_from")]
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

    [Column("effective_to")]
    public DateTime? EffectiveTo { get; set; }

    /// <summary>True while this interval is the current open price/tax assignment.</summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("changed_by")]
    public Guid ChangedBy { get; set; }

    [MaxLength(500)]
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Marks that the change is within RKSV-compliant catalog rules.</summary>
    [Column("is_rksv_compliant")]
    public bool IsRksvCompliant { get; set; } = true;

    [MaxLength(500)]
    [Column("rksv_note")]
    public string? RksvNote { get; set; }

    [Column("rksv_verified_at")]
    public DateTime? RksvVerifiedAt { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [ForeignKey(nameof(ProductId))]
    public virtual Product? Product { get; set; }

    [ForeignKey(nameof(OldTaxGroupId))]
    public virtual TaxGroup? OldTaxGroup { get; set; }

    [ForeignKey(nameof(NewTaxGroupId))]
    public virtual TaxGroup? NewTaxGroup { get; set; }
}
