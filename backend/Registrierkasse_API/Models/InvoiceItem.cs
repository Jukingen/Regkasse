using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse.Models
{
    [Table("invoice_items")]
    public class InvoiceItem : BaseEntity
    {
        [Required]
        [Column("invoice_id")]
        public Guid InvoiceId { get; set; }
        
        [ForeignKey("InvoiceId")]
        public virtual Invoice Invoice { get; set; } = null!;
        
        [Required]
        [Column("product_id")]
        public Guid ProductId { get; set; }
        
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
        
        [Required]
        [Column("quantity")]
        public decimal Quantity { get; set; }
        
        [Required]
        [Column("unit_price")]
        public decimal UnitPrice { get; set; }
        
        [Column("discount_amount")]
        public decimal DiscountAmount { get; set; }
        
        [Column("tax_amount")]
        public decimal TaxAmount { get; set; }
        
        [Column("total_amount")]
        public decimal TotalAmount { get; set; }
        
        [Column("notes")]
        [MaxLength(200)]
        public string Notes { get; set; } = string.Empty;
        
        [Column("is_active")]
        public new bool IsActive { get; set; } = true;
    }
} 