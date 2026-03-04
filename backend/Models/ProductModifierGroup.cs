using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Extra Zutaten grubu (örn. Saucen, Extras, Beilagen). Ürünlere M:N atanır.
    /// </summary>
    [Table("product_modifier_groups")]
    public class ProductModifierGroup : BaseEntity
    {
        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column("min_selections")]
        public int MinSelections { get; set; }

        [Column("max_selections")]
        public int? MaxSelections { get; set; }

        [Column("is_required")]
        public bool IsRequired { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }

        public virtual ICollection<ProductModifier> Modifiers { get; set; } = new List<ProductModifier>();
        public virtual ICollection<ProductModifierGroupAssignment> ProductAssignments { get; set; } = new List<ProductModifierGroupAssignment>();
    }
}
