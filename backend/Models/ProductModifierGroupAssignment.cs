using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Ürün ile modifier grubu arasında M:N atama. Hangi ürünün hangi grupları kullanabileceğini tanımlar.
    /// </summary>
    [Table("product_modifier_group_assignments")]
    public class ProductModifierGroupAssignment
    {
        [Column("product_id")]
        public Guid ProductId { get; set; }

        [Column("modifier_group_id")]
        public Guid ModifierGroupId { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;

        [ForeignKey("ModifierGroupId")]
        public virtual ProductModifierGroup ModifierGroup { get; set; } = null!;
    }
}
