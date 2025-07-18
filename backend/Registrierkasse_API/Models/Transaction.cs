using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public class Transaction : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string TransactionNumber { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(20)]
        public string PaymentMethod { get; set; } = "cash";

        [MaxLength(100)]
        public string? Reference { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "completed";

        [MaxLength(500)]
        public string? Description { get; set; }

        // Foreign keys
        public Guid? ReceiptId { get; set; }
        public Guid? InvoiceId { get; set; }
        public string? CashRegisterId { get; set; }
        public string? UserId { get; set; }

        // Navigation properties
        public virtual Receipt? Receipt { get; set; }
        public virtual Invoice? Invoice { get; set; }
        public virtual CashRegister? CashRegister { get; set; }
        public virtual ApplicationUser? User { get; set; }
    }
} 
