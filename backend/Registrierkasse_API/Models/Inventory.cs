using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public class Inventory : BaseEntity
    {
        [Required]
        public Guid ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int CurrentStock { get; set; }
        public int MinimumStock { get; set; }
        public int MaximumStock { get; set; }
        public DateTime? LastStockUpdate { get; set; }
        [MaxLength(200)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string? Location { get; set; }
    }
} 
