using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("carts")]
    public class Cart : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string CartId { get; set; } = string.Empty;

        public int? TableNumber { get; set; }

        [MaxLength(100)]
        public string? WaiterName { get; set; }

        public Guid? CustomerId { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime ExpiresAt { get; set; }

        [Required]
        public CartStatus Status { get; set; }

        // Navigation properties
        public virtual Customer? Customer { get; set; }
        public virtual ApplicationUser User { get; set; } = null!;
        public virtual ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    }

    public enum CartStatus
    {
        Active = 1,
        Completed = 2,
        Cancelled = 3,
        Expired = 4
    }
}
