using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("system_settings")]
    public class SystemSettings : BaseEntity
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

        [Required]
        [MaxLength(20)]
        public string CompanyTaxNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string DefaultLanguage { get; set; } = string.Empty;

        [Required]
        [MaxLength(3)]
        public string DefaultCurrency { get; set; } = string.Empty;

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
        [Column(TypeName = "jsonb")]
        public Dictionary<string, decimal> TaxRates { get; set; } = new();

        [MaxLength(50)]
        public string? ReceiptTemplate { get; set; }

        [MaxLength(10)]
        public string? InvoicePrefix { get; set; }

        [MaxLength(10)]
        public string? ReceiptPrefix { get; set; }

        public bool AutoBackup { get; set; }

        [Range(1, 168)]
        public int BackupFrequency { get; set; }

        [Range(1, 100)]
        public int MaxBackupFiles { get; set; }

        public DateTime? LastBackup { get; set; }

        public bool EmailNotifications { get; set; }

        public bool SmsNotifications { get; set; }

        [Column(TypeName = "jsonb")]
        public Dictionary<string, string>? EmailSettings { get; set; }

        [Column(TypeName = "jsonb")]
        public Dictionary<string, string>? SmsSettings { get; set; }
    }
}
