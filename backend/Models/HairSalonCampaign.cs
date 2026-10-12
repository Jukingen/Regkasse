using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Optional hair-salon campaign. Applied only when the tenant enables
/// <c>hair_salon_campaign</c>. Not a fiscal document.
/// </summary>
[Table("hair_salon_campaigns")]
public sealed class HairSalonCampaign : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("discount_percent", TypeName = "decimal(5,2)")]
    public decimal DiscountPercent { get; set; }

    /// <summary>Empty means every product. Otherwise only these product ids.</summary>
    [Column("applies_to_product_ids", TypeName = "jsonb")]
    public List<Guid> AppliesToProductIds { get; set; } = [];

    [Column("valid_from_utc")]
    public DateTime ValidFromUtc { get; set; }

    [Column("valid_until_utc")]
    public DateTime ValidUntilUtc { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
