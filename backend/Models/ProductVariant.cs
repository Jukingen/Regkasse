using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Color/size (or similar) variant of a catalog product. Metadata only:
/// not part of RKSV receipt content, TSE payloads, or DEP export.
/// </summary>
[Table("product_variants")]
public class ProductVariant : ITenantEntity
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Required]
    [Column("sku")]
    [MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [Column("variant_label")]
    [MaxLength(128)]
    public string VariantLabel { get; set; } = string.Empty;

    [Column("price_delta", TypeName = "numeric(18,2)")]
    public decimal PriceDelta { get; set; }

    [Column("stock")]
    public int Stock { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; }

    public virtual Tenant? Tenant { get; set; }

    public virtual Product? Product { get; set; }
}
