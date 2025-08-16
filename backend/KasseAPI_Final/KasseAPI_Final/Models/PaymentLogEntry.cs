using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    // English Description: Detailed log records for payment operations
    public class PaymentLogEntry : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string SessionId { get; set; } = string.Empty;

        public string? CartId { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string UserRole { get; set; } = string.Empty;

        public PaymentMethod? PaymentMethod { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Amount { get; set; }

        [Required]
        public PaymentLogStatus Status { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        [Column(TypeName = "jsonb")]
        public string? RequestData { get; set; }

        public Guid? CustomerId { get; set; }

        [MaxLength(100)]
        public string? CustomerName { get; set; }

        [MaxLength(255)]
        public string? CustomerEmail { get; set; }

        [MaxLength(20)]
        public string? CustomerPhone { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool? TseRequired { get; set; }

        [MaxLength(50)]
        public string? TaxNumber { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [Column(TypeName = "text")]
        public string? ErrorDetails { get; set; }

        [Column(TypeName = "jsonb")]
        public string? ResponseData { get; set; }

        public double? ProcessingTimeMs { get; set; }

        [MaxLength(500)]
        public string? TseSignature { get; set; }

        public Guid? InvoiceId { get; set; }

        [MaxLength(100)]
        public string? ReceiptNumber { get; set; }

        // Navigation properties
        [ForeignKey("CartId")]
        public virtual Cart? Cart { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        [ForeignKey("InvoiceId")]
        public virtual Invoice? Invoice { get; set; }
    }

    // Payment log statuses
    public enum PaymentLogStatus
    {
        Initiated,
        Pending,
        Success,
        Failed,
        Cancelled,
        Timeout,
        Refunded
    }
}
