using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("inventory_transactions")]
    public class InventoryTransaction : BaseEntity
    {
        [Required]
        public Guid InventoryId { get; set; }

        [Required]
        public TransactionType TransactionType { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; }

        // Navigation properties
        public virtual InventoryItem Inventory { get; set; } = null!;
    }

    public enum TransactionType
    {
        Open = 1,
        Close = 2,
        Restock = 3,
        Sale = 4,
        Adjustment = 5,
        Loss = 6,
        Return = 7,
        Transfer = 8
    }
}
