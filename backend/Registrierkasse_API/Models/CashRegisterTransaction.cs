using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse.Models
{
    public enum TransactionType
    {
        StartDay,
        EndDay,
        Sale,
        Refund,
        Deposit,
        Withdrawal,
        Adjustment
    }

    [Table("cash_register_transactions")]
    public class CashRegisterTransaction : BaseEntity
    {
        [Required]
        [Column("cash_register_id")]
        public Guid CashRegisterId { get; set; }
        
        [Required]
        [Column("transaction_type")]
        [MaxLength(20)]
        public string Type { get; set; } = TransactionType.Sale.ToString();
        
        [Required]
        [Column("amount")]
        public decimal Amount { get; set; }
        
        [Column("balance_before")]
        public decimal BalanceBefore { get; set; }
        
        [Column("balance_after")]
        public decimal BalanceAfter { get; set; }
        
        [Column("reference_number")]
        [MaxLength(50)]
        public string? ReferenceNumber { get; set; }
        
        [Column("description")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;
        
        [Column("is_active")]
        public new bool IsActive { get; set; } = true;
        
        [Required]
        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;
        
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;
        
        [Required]
        [Column("tse_signature")]
        public string TSESignature { get; set; } = string.Empty;
        
        [Required]
        [Column("tse_signature_counter")]
        public long TSESignatureCounter { get; set; }
        
        [Required]
        [Column("tse_time")]
        public DateTime TSETime { get; set; }
        
        [Column("invoice_id")]
        public Guid? InvoiceId { get; set; }
        
        [ForeignKey("InvoiceId")]
        public virtual Invoice? Invoice { get; set; }
        
        // Navigation properties
        [ForeignKey("CashRegisterId")]
        public virtual CashRegister CashRegister { get; set; } = null!;
    }
} 