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

        // Navigation properties - Doğru ForeignKey konfigürasyonu ile
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }
        
        [ForeignKey("UserId")]
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

    // Response modelleri
    public class CartResponse
    {
        public string CartId { get; set; } = string.Empty;
        public int? TableNumber { get; set; }
        public string? WaiterName { get; set; }
        public Guid? CustomerId { get; set; }
        public string? Notes { get; set; }
        public CartStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public List<CartItemResponse> Items { get; set; } = new List<CartItemResponse>();
        public int TotalItems { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TotalTax { get; set; }
        public decimal GrandTotal { get; set; }
    }

    public class CartItemResponse
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string? Notes { get; set; }
        public string TaxType { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
    }

    public class CreateCartRequest
    {
        public int? TableNumber { get; set; }
        public string? WaiterName { get; set; }
        public string? CustomerId { get; set; }
        public string? Notes { get; set; }
    }
}
