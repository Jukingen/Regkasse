using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("TseSignatures")]
    public class TseSignature
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Signature { get; set; } = string.Empty;

        [Required]
        public Guid CashRegisterId { get; set; }

        [Required]
        [MaxLength(100)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(50)]
        public string SignatureType { get; set; } = string.Empty; // Invoice, DailyClosing, MonthlyClosing, YearlyClosing

        public Guid? TseDeviceId { get; set; }

        [MaxLength(100)]
        public string? CertificateNumber { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ValidatedAt { get; set; }

        public bool IsValid { get; set; } = true;

        [MaxLength(500)]
        public string? ValidationError { get; set; }

        // Navigation properties
        [ForeignKey("CashRegisterId")]
        public virtual CashRegister? CashRegister { get; set; }

        [ForeignKey("TseDeviceId")]
        public virtual TseDevice? TseDevice { get; set; }
    }
}
