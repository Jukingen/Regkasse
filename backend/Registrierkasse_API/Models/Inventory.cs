using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse.Models
{
    [Table("inventory")]
    public class Inventory : BaseEntity
    {
        [Required]
        [Column("product_id")]
        public Guid ProductId { get; set; }
        
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
        
        [Required]
        [Column("current_stock")]
        public decimal CurrentStock { get; set; }
        
        [Column("minimum_stock")]
        public decimal MinimumStock { get; set; }
        
        [Column("maximum_stock")]
        public decimal MaximumStock { get; set; }
        
        [Column("last_stock_update")]
        public DateTime? LastStockUpdate { get; set; }
        
        [Column("location")]
        [MaxLength(100)]
        public string Location { get; set; } = string.Empty;
        
        [Column("notes")]
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
        
        [Column("is_active")]
        public new bool IsActive { get; set; } = true;
        
        public virtual ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
    }
} 