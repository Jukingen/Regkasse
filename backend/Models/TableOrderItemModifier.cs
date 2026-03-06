using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Persisted modifier selection for a table order line item. Price from DB (fiscal-safe).
    /// </summary>
    [Table("table_order_item_modifiers")]
    public class TableOrderItemModifier
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("table_order_item_id")]
        public Guid TableOrderItemId { get; set; }

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

        [ForeignKey("TableOrderItemId")]
        public virtual TableOrderItem TableOrderItem { get; set; } = null!;
    }
}
