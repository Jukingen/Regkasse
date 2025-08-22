using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("inventory")]
    public class InventoryItem : BaseEntity
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public int CurrentStock { get; set; }

        [Required]
        public int MinStockLevel { get; set; }

        public int? MaxStockLevel { get; set; }

        public int? ReorderPoint { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        public DateTime? LastRestocked { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Navigation properties
        // Product navigation property removed to prevent shadow property conflicts
        public virtual ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
    }
}
