using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("cash_register_transactions")]
    public class CashRegisterTransaction : BaseEntity
    {
        [Required]
        public Guid CashRegisterId { get; set; }
        
        [Required]
        public TransactionType TransactionType { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public DateTime TransactionDate { get; set; }

        // Navigation properties
        public virtual CashRegister CashRegister { get; set; } = null!;
        public virtual ApplicationUser User { get; set; } = null!;
    }
}
