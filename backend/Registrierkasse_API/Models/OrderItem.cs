using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse.Models
{
    public enum OrderItemStatus
    {
        Pending,
        InProgress,
        Ready,
        Served,
        Cancelled
    }

    [Table("order_items")]
    public class OrderItem : BaseEntity
    {
        [Required]
        [Column("order_id")]
        public Guid OrderId { get; set; }
        
        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; } = null!;
        
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
        
        [Column("notes")]
        [MaxLength(200)]
        public string Notes { get; set; } = string.Empty;
        
        [Column("is_active")]
        public new bool IsActive { get; set; } = true;
        
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = OrderItemStatus.Pending.ToString();
    }
} 