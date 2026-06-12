using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Tenant-wide company master data (single source of truth for POS receipts, invoices, and FinanzOnline).
    /// RKSV §8 mandatory receipt fields are mapped from <see cref="CompanyName"/>, <see cref="CompanyAddress"/>,
    /// and <see cref="CompanyTaxNumber"/>; values are snapshotted onto each <see cref="PaymentDetails"/> at sale time.
    /// </summary>
    [Table("company_settings")]
    public class CompanySettings : BaseEntity, ITenantEntity
    {
        [Required]
        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        /// <summary>RKSV §8 — Firmenname / Unternehmensbezeichnung.</summary>
        [Required]
        [MaxLength(100)]
        public string CompanyName { get; set; } = string.Empty;

        /// <summary>RKSV §8 — Firmenadresse / Sitz der gewerblichen Betriebsstätte.</summary>
        [Required]
        [MaxLength(200)]
        public string CompanyAddress { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? CompanyPhone { get; set; }

        [EmailAddress]
        [MaxLength(100)]
        public string? CompanyEmail { get; set; }

        [MaxLength(100)]
        public string? CompanyWebsite { get; set; }

        /// <summary>RKSV §8 — UID (Umsatzsteuer-Identifikationsnummer), format ATU12345678.</summary>
        [Required]
        [MaxLength(20)]
        public string CompanyTaxNumber { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? CompanyRegistrationNumber { get; set; }

        [MaxLength(20)]
        public string? CompanyVatNumber { get; set; }

        [MaxLength(100)]
        public string? CompanyLogo { get; set; }

        [MaxLength(500)]
        public string? CompanyDescription { get; set; }

        [Required]
        [Column(TypeName = "jsonb")]
        public Dictionary<string, string> BusinessHours { get; set; } = new();

        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        [MaxLength(20)]
        public string? ContactPhone { get; set; }

        [EmailAddress]
        [MaxLength(100)]
        public string? ContactEmail { get; set; }

        [MaxLength(100)]
        public string? BankName { get; set; }

        [MaxLength(50)]
        public string? BankAccountNumber { get; set; }

        [MaxLength(50)]
        public string? BankRoutingNumber { get; set; }

        [MaxLength(20)]
        public string? BankSwiftCode { get; set; }

        [MaxLength(50)]
        public string? PaymentTerms { get; set; }

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string Language { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TimeZone { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string DateFormat { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string TimeFormat { get; set; } = string.Empty;

        [Range(0, 4)]
        public int DecimalPlaces { get; set; }

        [Required]
        [MaxLength(50)]
        public string TaxCalculationMethod { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string InvoiceNumbering { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ReceiptNumbering { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string DefaultPaymentMethod { get; set; } = string.Empty;

        // FinanzOnline entegrasyonu için alanlar
        [MaxLength(500)]
        public string? FinanzOnlineApiUrl { get; set; }

        [MaxLength(100)]
        public string? FinanzOnlineUsername { get; set; }

        [MaxLength(100)]
        public string? FinanzOnlinePassword { get; set; }

        [MaxLength(12)]
        public string? FinanzOnlineTelematikId { get; set; }

        [MaxLength(24)]
        public string? FinanzOnlineHerstellerId { get; set; }

        public bool FinanzOnlineAutoSubmit { get; set; } = false;

        public int FinanzOnlineSubmitInterval { get; set; } = 60; // dakika

        public int FinanzOnlineRetryAttempts { get; set; } = 3;

        public bool FinanzOnlineEnableValidation { get; set; } = true;

        // FinanzOnline status fields
        public bool FinanzOnlineEnabled { get; set; } = false;

        public DateTime? LastFinanzOnlineSync { get; set; }

        public int? PendingInvoices { get; set; } = 0;

        // TSE cihazı ayarları
        [MaxLength(100)]
        public string? DefaultTseDeviceId { get; set; }

        public bool TseAutoConnect { get; set; } = false;

        public int TseConnectionTimeout { get; set; } = 30; // saniye

        /// <summary>
        /// When true (default), Vienna December uses Jahresbeleg flow instead of a separate Monatsbeleg row,
        /// and December Monatsbeleg may satisfy Jahresbeleg detection where configured.
        /// </summary>
        [Column("use_december_monatsbeleg_as_jahresbeleg")]
        public bool UseDecemberMonatsbelegAsJahresbeleg { get; set; } = true;
    }
}
