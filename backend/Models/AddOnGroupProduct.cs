using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Faz 1: Suggested Add-On Group içinde product referansı. Fiyat/vergi Product tablosunda; bu tablo sadece grup ↔ product bağlantısı + sıra.
    /// Mevcut product_modifier_groups (ModifierGroupId) ile ilişkilidir; ayrı addon_groups tablosu yok.
    /// </summary>
    [Table("addon_group_products")]
    public class AddOnGroupProduct
    {
        [Required]
        [Column("modifier_group_id")]
        public Guid ModifierGroupId { get; set; }

        [Required]
        [Column("product_id")]
        public Guid ProductId { get; set; }

        [Required]
        [Column("sort_order")]
        public int SortOrder { get; set; }

        [ForeignKey("ModifierGroupId")]
        public virtual ProductModifierGroup ModifierGroup { get; set; } = null!;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
    }
}
