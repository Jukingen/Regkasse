using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public class InventoryTransaction : BaseEntity
    {
        [Required]
        public string InventoryId { get; set; } = string.Empty;
        public virtual Inventory? Inventory { get; set; }

        public int QuantityChange { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    }
} 
