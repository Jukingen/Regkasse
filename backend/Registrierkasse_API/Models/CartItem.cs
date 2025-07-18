using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public class CartItem : BaseEntity
    {
        [Required]
        public string CartId { get; set; } = string.Empty;
        public virtual Cart Cart { get; set; } = null!;

        [Required]
        public Guid ProductId { get; set; }
        public virtual Product Product { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [MaxLength(200)]
        public string? Notes { get; set; }

        public bool IsModified { get; set; } = false; // Price or quantity modified
        public DateTime? ModifiedAt { get; set; }

        // For tracking modifications
        [Column(TypeName = "decimal(18,2)")]
        public decimal OriginalUnitPrice { get; set; }

        public int OriginalQuantity { get; set; }
    }
} 