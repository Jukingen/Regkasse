using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public class OrderItem : BaseEntity
    {
        [Required]
        public Guid ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [MaxLength(200)]
        public string? Notes { get; set; }

        public string OrderId { get; set; } = string.Empty;
        public virtual Order Order { get; set; } = null!;
    }
} 
