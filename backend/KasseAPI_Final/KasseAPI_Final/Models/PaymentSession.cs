using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    // English Description: Temporary session information for payment operations
    public class PaymentSession : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string SessionId { get; set; } = string.Empty;

        [Required]
        public string CartId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string UserRole { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        public Guid? CustomerId { get; set; }

        [MaxLength(100)]
        public string? CustomerName { get; set; }

        [MaxLength(255)]
        public string? CustomerEmail { get; set; }

        [MaxLength(20)]
        public string? CustomerPhone { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Required]
        public bool TseRequired { get; set; }

        [MaxLength(50)]
        public string? TaxNumber { get; set; }

        [Required]
        public PaymentSessionStatus Status { get; set; }



        [Required]
        public DateTime ExpiresAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        [MaxLength(500)]
        public string? TseSignature { get; set; }

        public Guid? InvoiceId { get; set; }

        [MaxLength(100)]
        public string? ReceiptNumber { get; set; }

        [MaxLength(500)]
        public string? ErrorDetails { get; set; }

        public int? RetryCount { get; set; }

        [MaxLength(100)]
        public string? LastErrorType { get; set; }

        public DateTime? LastErrorAt { get; set; }

        // İptal işlemi için gerekli alanlar
        public DateTime? CancelledAt { get; set; }

        [MaxLength(100)]
        public string? CancelledBy { get; set; }

        [MaxLength(500)]
        public string? CancellationReason { get; set; }

        // Navigation properties
        [ForeignKey("CartId")]
        public virtual Cart Cart { get; set; } = null!;

        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        [ForeignKey("InvoiceId")]
        public virtual Invoice? Invoice { get; set; }
    }

    // Payment session statuses
    public enum PaymentSessionStatus
    {
        Initiated,   // Started
        Processing,  // Processing
        Completed,   // Completed
        Failed,      // Failed
        Cancelled,   // Cancelled
        Expired,     // Expired
        Pending      // Waiting
    }
}
