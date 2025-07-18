using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public class Cart
    {
        [Key]
        [Required]
        public string CartId { get; set; } = string.Empty; // Unique cart identifier

        [MaxLength(50)]
        public string? TableNumber { get; set; } // For restaurant orders

        [MaxLength(50)]
        public string? WaiterName { get; set; }

        public Guid? CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }

        public string? UserId { get; set; } // Cashier who created the cart
        public virtual ApplicationUser? User { get; set; }

        public Guid? CashRegisterId { get; set; }
        public virtual CashRegister? CashRegister { get; set; }

        // Masa yönetimi için yeni alanlar
        public Guid? TableId { get; set; }
        public virtual Table? Table { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        // Yeni hesaplama alanları
        [Column(TypeName = "decimal(18,2)")]
        public decimal ServiceChargeAmount { get; set; } = 0; // Servis ücreti

        [Column(TypeName = "decimal(18,2)")]
        public decimal TipAmount { get; set; } = 0; // Bahşiş

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        // Bölünmüş hesap bilgileri
        [Column("split_count")]
        public int SplitCount { get; set; } = 1; // Kaç kişiye bölünecek

        [Column("split_amount", TypeName = "decimal(18,2)")]
        public decimal SplitAmount { get; set; } = 0; // Kişi başı tutar

        [Column("payment_methods")]
        public string PaymentMethods { get; set; } = ""; // JSON array of payment methods

        public Guid? AppliedCouponId { get; set; }
        public virtual Coupon? AppliedCoupon { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public CartStatus Status { get; set; } = CartStatus.Active;

        public DateTime? ExpiresAt { get; set; } // Cart expiration time

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    }

    public enum CartStatus
    {
        Active,
        Completed,
        Cancelled,
        Expired
    }
} 