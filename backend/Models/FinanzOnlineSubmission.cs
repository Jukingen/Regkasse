using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("FinanzOnlineSubmissions")]
    public class FinanzOnlineSubmission : ITenantEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant? Tenant { get; set; }

        [Required]
        public Guid InvoiceId { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "jsonb")]
        public string RequestPayloadJson { get; set; } = "{}";

        public string ResponseStatusCode { get; set; } = string.Empty;

        [Column(TypeName = "jsonb")]
        public string ResponseBodyJson { get; set; } = "{}";

        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

        // Navigation property (optional, dependent on Invoice)
        // [ForeignKey("InvoiceId")]
        // public virtual Invoice Invoice { get; set; }
    }
}
