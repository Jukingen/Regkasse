using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Fiscal daily/monthly/yearly closing record. RKSV operational fields
    /// (<see cref="CashierName"/>, <see cref="ShiftNumber"/>) are stamped at close time.
    /// Company presentation fields are assembled at report time via enricher + company settings.
    /// </summary>
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

        /// <summary>RKSV Mitarbeiter — display name of cashier who performed the closing.</summary>
        [MaxLength(200)]
        [Column("cashier_name")]
        public string CashierName { get; set; } = string.Empty;

        /// <summary>RKSV Schicht-Nr. — sequential shift index for the cash register.</summary>
        [Column("shift_number")]
        public int ShiftNumber { get; set; }

        [Required]
        public DateTime ClosingDate { get; set; }

        /// <summary>
        /// True when this daily closing covers a past Vienna business day (nachträglich).
        /// <see cref="CreatedAt"/> remains the real UTC creation instant and is never backdated.
        /// </summary>
        [Column("is_backdated")]
        public bool IsBackdated { get; set; }

        /// <summary>
        /// Operator-provided reason for a late (nachträglich) daily closing; null for same-day closings.
        /// Documented for Betriebsprüfung transparency; does not affect TSE signing time.
        /// </summary>
        [MaxLength(500)]
        [Column("late_creation_reason")]
        public string? LateCreationReason { get; set; }

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
