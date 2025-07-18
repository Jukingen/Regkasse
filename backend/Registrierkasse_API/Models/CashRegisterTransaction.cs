using System.ComponentModel.DataAnnotations;

namespace Registrierkasse_API.Models
{
    public class CashRegisterTransaction : BaseEntity
    {
        [Required]
        public string CashRegisterId { get; set; } = string.Empty;
        public virtual CashRegister CashRegister { get; set; } = null!;

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public TransactionType TransactionType { get; set; }

        [StringLength(200)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? Reference { get; set; }

        public string? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public string TSESignature { get; set; } = string.Empty;
        public int TSESignatureCounter { get; set; }
        public DateTime? TSETime { get; set; }
    }
} 
