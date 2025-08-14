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

        // Navigation properties
        public virtual Invoice Invoice { get; set; } = null!;
        public virtual Customer? Customer { get; set; }
    }



    public enum PaymentStatus
    {
        Pending = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4,
        Refunded = 5
    }
}
