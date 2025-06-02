using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Registrierkasse.Models
{
    [Table("invoices")]
    public class Invoice : BaseEntity
    {
        [Required]
        [Column("invoice_number")]
        [MaxLength(20)]
        public string InvoiceNumber { get; set; } = string.Empty;
        
        [Required]
        [Column("cash_register_id")]
        public Guid CashRegisterId { get; set; }
        
        [ForeignKey("CashRegisterId")]
        public virtual CashRegister CashRegister { get; set; } = null!;
        
        [Column("customer_id")]
        public Guid? CustomerId { get; set; }
        
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }
        
        [Column("order_id")]
        public Guid? OrderId { get; set; }
        
        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }
        
        [Required]
        [Column("invoice_date")]
        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
        
        [Required]
        [Column("total_amount")]
        public decimal TotalAmount { get; set; }
        
        [Required]
        [Column("tax_amount")]
        public decimal TaxAmount { get; set; }
        
        [Required]
        [Column("payment_method")]
        [MaxLength(20)]
        public PaymentMethod PaymentMethod { get; set; }
        
        [Column("waiter_name")]
        [MaxLength(100)]
        public string WaiterName { get; set; } = string.Empty;
        
        [Column("notes")]
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
        
        [Column("is_active")]
        public new bool IsActive { get; set; } = true;
        
        [Column("receipt_number")]
        [MaxLength(20)]
        public string ReceiptNumber { get; set; } = string.Empty;
        
        [Column("tse_signature")]
        [MaxLength(255)]
        public string TseSignature { get; set; } = string.Empty;
        
        [Column("is_printed")]
        public bool IsPrinted { get; set; }
        
        [Column("tax_details")]
        public JsonDocument TaxDetails { get; set; } = null!;
        
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";
        
        public virtual ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
        
        public virtual FinanceOnline? FinanceOnline { get; set; }
    }

    public enum InvoiceStatus
    {
        Pending,
        Completed,
        Cancelled,
        Refunded
    }
} 