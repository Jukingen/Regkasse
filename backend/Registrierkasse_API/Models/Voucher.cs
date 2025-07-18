using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    [Table("vouchers")]
    public class Voucher : BaseEntity
    {
        [Required]
        [Column("voucher_number")]
        [MaxLength(50)]
        public string VoucherNumber { get; set; } = string.Empty;
        
        [Required]
        [Column("amount")]
        public decimal Amount { get; set; }
        
        [Required]
        [Column("issue_date")]
        public DateTime IssueDate { get; set; }
        
        [Required]
        [Column("expiry_date")]
        public DateTime ExpiryDate { get; set; }
        
        [Required]
        [Column("status")]
        public VoucherStatus Status { get; set; } = VoucherStatus.Active;
        
        [Column("customer_id")]
        public Guid? CustomerId { get; set; }
        
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }
        
        [Column("notes")]
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
        
        [Column("used_date")]
        public DateTime? UsedDate { get; set; }
        
        [Column("used_by_order_id")]
        public Guid? UsedByOrderId { get; set; }
        
        [ForeignKey("UsedByOrderId")]
        public virtual Order? UsedByOrder { get; set; }
    }
    
    public enum VoucherStatus
    {
        Active,
        Used,
        Expired,
        Cancelled
    }
} 
