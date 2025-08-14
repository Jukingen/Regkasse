using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("generated_receipts")]
    public class GeneratedReceipt : BaseEntity
    {
        [Required]
        public Guid TemplateId { get; set; }

        [Required]
        [MaxLength(10)]
        public string Language { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TemplateType { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "text")]
        public string GeneratedContent { get; set; } = string.Empty;

        [Required]
        public DateTime GeneratedAt { get; set; }

        // Navigation property
        public virtual ReceiptTemplate? Template { get; set; }
    }
}
