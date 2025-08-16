using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Ödeme kalemi
    /// </summary>
    [Table("payment_items")]
    public class PaymentItem : BaseEntity
    {
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
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string TaxType { get; set; } = "standard";
        
        [Required]
        [Column(TypeName = "decimal(5,4)")]
        public decimal TaxRate { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }
        
        // Navigation property
        public virtual Product? Product { get; set; }
    }
}
