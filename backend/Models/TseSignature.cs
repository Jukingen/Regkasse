using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("TseSignatures")]
    public class TseSignature : ITenantEntity
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant? Tenant { get; set; }

        [Required]
        [Column(TypeName = "text")]
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

        // RKSV verification normalization (Phase 1) - nullable
        [MaxLength(50)]
        public string? SignatureFormat { get; set; }

        [Column(TypeName = "text")]
        public string? JwsHeader { get; set; }

        [Column(TypeName = "text")]
        public string? JwsPayload { get; set; }

        [Column(TypeName = "text")]
        public string? JwsSignature { get; set; }

        [MaxLength(50)]
        public string? Provider { get; set; }

        [MaxLength(100)]
        public string? CorrelationId { get; set; }

        // Navigation properties
        [ForeignKey("CashRegisterId")]
        public virtual CashRegister? CashRegister { get; set; }

        [ForeignKey("TseDeviceId")]
        public virtual TseDevice? TseDevice { get; set; }
    }
}
