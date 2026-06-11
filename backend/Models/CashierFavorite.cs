using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Per-cashier product shortcut for POS quick-add. Scoped by tenant and AspNetUsers id.
/// </summary>
[Table("cashier_favorites")]
public class CashierFavorite : BaseEntity, ITenantEntity
{
    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("cashier_id")]
    public string CashierId { get; set; } = string.Empty;

    [Required]
    [Column("product_id")]
    public Guid ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public virtual Product Product { get; set; } = null!;

    [Column("sort_order")]
    public int SortOrder { get; set; }
}
