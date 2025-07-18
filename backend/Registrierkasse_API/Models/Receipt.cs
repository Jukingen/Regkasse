using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public class Receipt : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        [RegularExpression(@"^\d{8}-[0-9a-fA-F\-]{36}$", ErrorMessage = "Fiş numarası formatı: {tarih}-{uuid} olmalı.")]
        public string ReceiptNumber { get; set; } = string.Empty;

        public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [MaxLength(500)]
        public string? TseSignature { get; set; }

        [MaxLength(50)]
        public string? KassenId { get; set; }

        [MaxLength(20)]
        public string PaymentMethod { get; set; } = "cash";

        public bool IsPrinted { get; set; } = false;

        public bool IsCancelled { get; set; } = false;

        [MaxLength(500)]
        public string? CancellationReason { get; set; }

        // Foreign keys
        public string? CashRegisterId { get; set; }
        public string? UserId { get; set; }

        // Navigation properties
        public virtual CashRegister? CashRegister { get; set; }
        public virtual ApplicationUser? User { get; set; }
        public virtual ICollection<ReceiptItem> Items { get; set; } = new List<ReceiptItem>();
    }
} 
