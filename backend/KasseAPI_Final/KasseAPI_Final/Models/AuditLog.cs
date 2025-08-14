using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("audit_logs")]
    public class AuditLog : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string EntityType { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string EntityId { get; set; } = string.Empty;

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? UserName { get; set; }

        [MaxLength(4000)]
        public string? OldValues { get; set; }

        [MaxLength(4000)]
        public string? NewValues { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }
    }
}
