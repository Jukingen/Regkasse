using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("localization_settings")]
    public class LocalizationSettings : BaseEntity
    {
        [Required]
        [MaxLength(10)]
        public string DefaultLanguage { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "jsonb")]
        public List<string> SupportedLanguages { get; set; } = new();

        [Required]
        [MaxLength(3)]
        public string DefaultCurrency { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "jsonb")]
        public List<string> SupportedCurrencies { get; set; } = new();

        [Required]
        [MaxLength(50)]
        public string DefaultTimeZone { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "jsonb")]
        public List<string> SupportedTimeZones { get; set; } = new();

        [Required]
        [MaxLength(20)]
        public string DefaultDateFormat { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string DefaultTimeFormat { get; set; } = string.Empty;

        [Range(0, 4)]
        public int DefaultDecimalPlaces { get; set; }

        [Required]
        [MaxLength(50)]
        public string NumberFormat { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "jsonb")]
        public Dictionary<string, string> DateFormatOptions { get; set; } = new();

        [Required]
        [Column(TypeName = "jsonb")]
        public Dictionary<string, string> TimeFormatOptions { get; set; } = new();

        [Required]
        [Column(TypeName = "jsonb")]
        public Dictionary<string, string> CurrencySymbols { get; set; } = new();
    }
}
