using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Tekil modifier (örn. Ketchup, Mayo, Extra Fleisch). Bir gruba bağlıdır.
    /// DEPRECATED: Add-on = Product (IsSellableAddOn) is the active model. This entity is kept for:
    /// - Historical data (old receipts, audit)
    /// - ModifierMigrationService (migrate to products)
    /// - Read-only API responses (group.modifiers) until migration complete. Do not add new writes.
    /// </summary>
    [Table("product_modifiers")]
    public class ProductModifier : BaseEntity
    {
        [Required]
        [Column("modifier_group_id")]
        public Guid ModifierGroupId { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Column("price", TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required]
        [Column("tax_type")]
        public int TaxType { get; set; } = 1;

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [ForeignKey("ModifierGroupId")]
        public virtual ProductModifierGroup ModifierGroup { get; set; } = null!;
    }
}
