using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse.Models
{
    public enum OrderStatus
    {
        Pending,
        InProgress,
        Completed,
        Cancelled
    }

    [Table("orders")]
    public class Order : BaseEntity
    {
        [Required]
        [Column("order_number")]
        [MaxLength(20)]
        public string OrderNumber { get; set; } = string.Empty;
        
        [Column("customer_id")]
        public Guid? CustomerId { get; set; }
        
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }
        
        [Column("table_number")]
        public int TableNumber { get; set; }
        
        [Column("waiter_name")]
        [MaxLength(100)]
        public string WaiterName { get; set; } = string.Empty;
        
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = OrderStatus.Pending.ToString();
        
        [Column("total_amount")]
        public decimal TotalAmount { get; set; }
        
        [Column("tax_amount")]
        public decimal TaxAmount { get; set; }
        
        [Column("discount_amount")]
        public decimal DiscountAmount { get; set; }
        
        [Column("notes")]
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
        
        [Column("is_active")]
        public new bool IsActive { get; set; } = true;
        
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        
        public virtual Invoice? Invoice { get; set; }
    }
}