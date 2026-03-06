using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Persisted modifier selection for a cart line item. Price from DB (fiscal-safe).
    /// </summary>
    [Table("cart_item_modifiers")]
    public class CartItemModifier
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("cart_item_id")]
        public Guid CartItemId { get; set; }

        [Required]
        [Column("modifier_id")]
        public Guid ModifierId { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("price", TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Column("modifier_group_id")]
        public Guid? ModifierGroupId { get; set; }

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; } = 1;

        [ForeignKey("CartItemId")]
        public virtual CartItem CartItem { get; set; } = null!;
    }
}
