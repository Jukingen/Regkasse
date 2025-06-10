using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse.Models
{
    public class AuditLog : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty; // CREATE, UPDATE, DELETE, LOGIN, etc.

        [Required]
        [MaxLength(100)]
        public string EntityType { get; set; } = string.Empty; // Invoice, Product, Customer, etc.

        [MaxLength(36)]
        public string? EntityId { get; set; } // UUID of the affected entity

        [Required]
        [MaxLength(36)]
        public string UserId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? UserName { get; set; }

        [MaxLength(50)]
        public string? UserRole { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [Column(TypeName = "jsonb")]
        public string? OldValues { get; set; } // JSON serialized old values

        [Column(TypeName = "jsonb")]
        public string? NewValues { get; set; } // JSON serialized new values

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(20)]
        public string? Status { get; set; } = "SUCCESS"; // SUCCESS, FAILED, WARNING

        [MaxLength(500)]
        public string? ErrorMessage { get; set; }

        [Column(TypeName = "jsonb")]
        public string? AdditionalData { get; set; } // Extra context data
    }
} 