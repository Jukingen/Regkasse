using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Point-in-time price + tax-group version for a product (RKSV audit / historical lookup).
/// </summary>
[Table("product_price_versions")]
public class ProductPriceVersion : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("price", TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    [Column("tax_group_id")]
    public Guid TaxGroupId { get; set; }

    [Column("valid_from")]
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;

    [Column("valid_to")]
    public DateTime? ValidTo { get; set; }

    [Column("is_current")]
    public bool IsCurrent { get; set; } = true;

    /// <summary>Semantic version label, e.g. "1.0", "2.0".</summary>
    [Column("version", TypeName = "text")]
    public string? Version { get; set; } = "1.0";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [ForeignKey(nameof(ProductId))]
    public virtual Product? Product { get; set; }

    [ForeignKey(nameof(TaxGroupId))]
    public virtual TaxGroup? TaxGroup { get; set; }
}
