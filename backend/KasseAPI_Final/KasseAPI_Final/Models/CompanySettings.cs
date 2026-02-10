using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("company_settings")]
    public class CompanySettings : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string CompanyName { get; set; } = string.Empty;

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
    }
}
