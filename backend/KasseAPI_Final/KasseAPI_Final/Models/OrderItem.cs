using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("order_items")]
    public class OrderItem : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string OrderId { get; set; } = string.Empty;

        [Required]
        public Guid ProductId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [MaxLength(500)]
        public string? SpecialNotes { get; set; }

        [MaxLength(500)]
        public string? ProductDescription { get; set; }

        [MaxLength(100)]
        public string? ProductCategory { get; set; }

        // Navigation properties
        public virtual Order Order { get; set; } = null!;
        // Product navigation property removed to prevent shadow property conflicts
    }
}
