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
        /// <summary>Seçili modifier'lar (fiyat DB'den; RKSV güvenli).</summary>
        public virtual ICollection<CartItemModifier> Modifiers { get; set; } = new List<CartItemModifier>();
    }
}
