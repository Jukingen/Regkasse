using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("FinanzOnlineErrors")]
    public class FinanzOnlineError
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ErrorType { get; set; } = string.Empty; // Submission, Connection, Validation

        [Required]
        [MaxLength(500)]
        public string ErrorMessage { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ReferenceId { get; set; }

        [Required]
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        public bool IsResolved { get; set; } = false;

        public DateTime? ResolvedAt { get; set; }

        [MaxLength(100)]
        public string? ResolvedBy { get; set; }

        [MaxLength(500)]
        public string? ResolutionNotes { get; set; }

        public Guid? CashRegisterId { get; set; }

        [MaxLength(100)]
        public string? InvoiceNumber { get; set; }

        public int RetryCount { get; set; } = 0;

        public DateTime? LastRetryAt { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Active"; // Active, Resolved, Ignored

        // Navigation properties
        [ForeignKey("CashRegisterId")]
        public virtual CashRegister? CashRegister { get; set; }
    }
}
