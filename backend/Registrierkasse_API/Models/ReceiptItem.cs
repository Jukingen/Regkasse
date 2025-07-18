using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public class ReceiptItem : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Total { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        public TaxType TaxType { get; set; } = TaxType.Standard;

        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; }

        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }

        // Foreign keys
        public Guid ReceiptId { get; set; }
        public Guid? ProductId { get; set; }

        // Navigation properties
        public virtual Receipt Receipt { get; set; } = null!;
        public virtual Product? Product { get; set; }
    }
} 
