using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("DailyClosings")]
    public class DailyClosing
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(50)]
        public Guid CashRegisterId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public DateTime ClosingDate { get; set; }

        [Required]
        [MaxLength(20)]
        public string ClosingType { get; set; } = string.Empty; // Daily, Monthly, Yearly

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalTaxAmount { get; set; }

        [Required]
        public int TransactionCount { get; set; }

        [Required]
        [MaxLength(500)]
        public string TseSignature { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = string.Empty; // Completed, Failed, Pending

        [MaxLength(20)]
        public string? FinanzOnlineStatus { get; set; } // Submitted, Failed, Pending

        [MaxLength(500)]
        public string? FinanzOnlineError { get; set; }

        [MaxLength(100)]
        public string? FinanzOnlineReferenceId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        [ForeignKey("CashRegisterId")]
        public virtual CashRegister? CashRegister { get; set; }
    }
}
