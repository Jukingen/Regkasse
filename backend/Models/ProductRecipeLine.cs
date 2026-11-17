using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>One ingredient row of a product recipe. Quantity is in the product's own unit.</summary>
[Table("product_recipe_lines")]
public class ProductRecipeLine : BaseEntity, ITenantEntity
{
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Required]
    [Column("ingredient_name")]
    [MaxLength(200)]
    public string IngredientName { get; set; } = string.Empty;

    [Column("quantity", TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }
}
