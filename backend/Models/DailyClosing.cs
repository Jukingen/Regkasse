using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("DailyClosings")]
    public class DailyClosing : ITenantEntity
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant? Tenant { get; set; }

        [Required]
        public Guid CashRegisterId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public DateTime ClosingDate { get; set; }

        [Required]
        [MaxLength(20)]
        public string ClosingType { get; set; } = string.Empty; // Daily, Monthly, Yearly

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalTaxAmount { get; set; }

        [Required]
        public int TransactionCount { get; set; }

        [Column(TypeName = "text")]
        public string? TseSignature { get; set; }

        [MaxLength(50)]
        [Column("tse_signature_timestamp")]
        public string? TseSignatureTimestamp { get; set; }

        /// <summary>SHA-1 thumbprint of the TSE signing certificate used for <see cref="TseSignature"/> (DEP grouping).</summary>
        [MaxLength(64)]
        [Column("certificate_thumbprint")]
        public string? CertificateThumbprint { get; set; }

        /// <summary>Alias for <see cref="CertificateThumbprint"/> (RKSV Phase 1 naming).</summary>
        [NotMapped]
        public string? TseCertificateThumbprint
        {
            get => CertificateThumbprint;
            set => CertificateThumbprint = value;
        }

        [Column(TypeName = "text")]
        public string? PreviousSignature { get; set; }

        public int SignatureChainLength { get; set; }

        public bool IsSimulated { get; set; }

        [MaxLength(20)]
        public string? Environment { get; set; }

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

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = string.Empty; // Completed, Failed, Pending

        [MaxLength(20)]
        public string? FinanzOnlineStatus { get; set; } // Submitted, Failed, Pending

        [MaxLength(500)]
        public string? FinanzOnlineError { get; set; }

        [MaxLength(100)]
        public string? FinanzOnlineReferenceId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        [ForeignKey("CashRegisterId")]
        public virtual CashRegister? CashRegister { get; set; }
    }
}
