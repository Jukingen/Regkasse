using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse.Models
{
    public enum InventoryTransactionType
    {
        Restock,
        Sale,
        Adjustment,
        Return,
        Waste,
        Transfer
    }

    [Table("inventory_transactions")]
    public class InventoryTransaction : BaseEntity
    {
        [Required]
        [Column("inventory_id")]
        public Guid InventoryId { get; set; }
        
        [ForeignKey("InventoryId")]
        public virtual Inventory Inventory { get; set; } = null!;
        
        [Required]
        [Column("transaction_type")]
        [MaxLength(20)]
        public string Type { get; set; } = InventoryTransactionType.Restock.ToString();
        
        [Required]
        [Column("quantity")]
        public decimal Quantity { get; set; }
        
        [Column("reference")]
        [MaxLength(100)]
        public string Reference { get; set; } = string.Empty;
        
        [Column("notes")]
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
        
        [Column("is_active")]
        public new bool IsActive { get; set; } = true;

        [Column("user_id")]
        public string? UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
    }
} 