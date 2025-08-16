using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("payment_details")]
    public class PaymentDetails : BaseEntity
    {
        [Required]
        public Guid CustomerId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string? Notes { get; set; }
        
        [MaxLength(100)]
        public string? TransactionId { get; set; }
        
        [MaxLength(100)]
        public string? TseSignature { get; set; }
        
        // Tax details as JSON
        [Column(TypeName = "jsonb")]
        public Dictionary<string, decimal> TaxDetails { get; set; } = new();
        
        // Payment items
        public List<PaymentItem> Items { get; set; } = new();
        
        // Refund related properties
        public Guid? OriginalPaymentId { get; set; }
        [MaxLength(500)]
        public string? CancellationReason { get; set; }
        public bool IsRefund { get; set; } = false;
        
        // Cancellation and refund details
        public DateTime? CancelledAt { get; set; }
        [MaxLength(500)]
        public string? RefundReason { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal? RefundAmount { get; set; }
        
        // FinanzOnline integration
        public bool IsFinanzOnlineSent { get; set; } = false;
        public DateTime? FinanzOnlineSentAt { get; set; }
        
        // Navigation properties
        public virtual Customer? Customer { get; set; }
        public virtual PaymentDetails? OriginalPayment { get; set; }
        public virtual ICollection<PaymentDetails> Refunds { get; set; } = new List<PaymentDetails>();
    }
}
