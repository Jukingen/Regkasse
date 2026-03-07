using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Ödeme kalemi. Stored only in payment_details.PaymentItems (JSON); not a mapped table.
    /// </summary>
    public class PaymentItem : BaseEntity
    {
        [Required]
        public Guid ProductId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ProductName { get; set; } = string.Empty;
        
        [Required]
        public int Quantity { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }
        
        [Required]
        public int TaxType { get; set; } = 1;
        
        [Required]
        [Column(TypeName = "decimal(5,4)")]
        public decimal TaxRate { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        /// <summary>Satır net tutarı (CartMoneyHelper.LineNet); rounding tutarlılığı için saklanır.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineNet { get; set; }

        /// <summary>Phase 2 deprecated: Legacy embedded modifiers (fiş/receipt). Yeni akış: add-on = ayrı PaymentItem. Read-only for existing payment history.</summary>
        public List<PaymentItemModifierSnapshot> Modifiers { get; set; } = new();
    }

    /// <summary>Phase 2 deprecated: Legacy modifier snapshot in PaymentItems JSON. Read-only for receipt/history.</summary>
    public class PaymentItemModifierSnapshot
    {
        public Guid ModifierId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public int TaxType { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal LineNet { get; set; }
    }
}
