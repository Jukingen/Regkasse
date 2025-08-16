using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("payment_details")]
    public class PaymentDetails : BaseEntity
    {
        [Required]
        public Guid InvoiceId { get; set; }

        public Guid? CustomerId { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; }

        [Required]
        public PaymentStatus Status { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string? TransactionId { get; set; }

        // Refund related properties
        public Guid? OriginalPaymentId { get; set; } // Reference to original payment for refunds
        [MaxLength(500)]
        public string? RefundReason { get; set; } // Reason for refund
        public PaymentMethod? RefundMethod { get; set; } // Method used for refund
        [MaxLength(100)]
        public string? RefundedBy { get; set; } // User who processed the refund
        public DateTime? RefundedAt { get; set; } // When the refund was processed

        // Navigation properties
        public virtual Invoice? Invoice { get; set; }
        public virtual Customer? Customer { get; set; }
        public virtual PaymentDetails? OriginalPayment { get; set; } // Navigation to original payment
        public virtual ICollection<PaymentDetails> Refunds { get; set; } = new List<PaymentDetails>(); // Collection of refunds for this payment
    }

    public enum PaymentStatus
    {
        Pending,
        Completed,
        Failed,
        Cancelled,
        Refunded,
        PartiallyRefunded,
        FullyRefunded
    }
}
