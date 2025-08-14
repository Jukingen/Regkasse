using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("receipt_templates")]
    public class ReceiptTemplate : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string TemplateName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TemplateType { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string Language { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? HeaderTemplate { get; set; }

        [MaxLength(2000)]
        public string? FooterTemplate { get; set; }

        [MaxLength(1000)]
        public string? ItemTemplate { get; set; }

        [MaxLength(500)]
        public string? TaxTemplate { get; set; }

        [MaxLength(500)]
        public string? TotalTemplate { get; set; }

        [MaxLength(500)]
        public string? PaymentTemplate { get; set; }

        [MaxLength(1000)]
        public string? CustomerTemplate { get; set; }

        [MaxLength(1000)]
        public string? CompanyTemplate { get; set; }

        [Column(TypeName = "jsonb")]
        public Dictionary<string, string>? CustomFields { get; set; }

        public bool IsDefault { get; set; }
    }
}
