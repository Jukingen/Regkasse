using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("cart_items")]
    public class CartItem : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string CartId { get; set; } = string.Empty;

        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Navigation properties
        public virtual Cart Cart { get; set; } = null!;
        // Product navigation property removed to prevent shadow property conflicts
    }
}
