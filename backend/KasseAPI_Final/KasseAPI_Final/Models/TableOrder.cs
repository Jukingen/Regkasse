using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Masa siparişlerinin kalıcı saklanması için özel model
    /// Cart'tan farklı olarak, masa bazlı sipariş durumlarını ve geçmişini tutar
    /// RKSV uyumlu audit trail sağlar
    /// </summary>
    [Table("table_orders")]
    public class TableOrder : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string TableOrderId { get; set; } = string.Empty;

        [Required]
        public int TableNumber { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty; // RKSV: Hangi kullanıcının siparişi

        [MaxLength(100)]
        public string? WaiterName { get; set; }

        [MaxLength(100)]
        public string? CustomerName { get; set; }

        [MaxLength(20)]
        public string? CustomerPhone { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Required]
        public TableOrderStatus Status { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        // RKSV uyumlu tarih/saat izleme
        [Required]
        public DateTime OrderStartTime { get; set; } // Sipariş başlama zamanı

        public DateTime? LastModifiedTime { get; set; } // Son değişiklik zamanı

        public DateTime? CompletedTime { get; set; } // Tamamlanma zamanı

        // Cart ile ilişki (aktif sepet)
        public string? CartId { get; set; }

        // Session tracking
        [MaxLength(100)]
        public string? SessionId { get; set; } // F5 sonrası session takibi için

        // Audit trail için
        [MaxLength(1000)]
        public string? StatusHistory { get; set; } // JSON format sipariş durumu geçmişi

        public Guid? CustomerId { get; set; }

        // Navigation properties
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }
        
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;
        
        [ForeignKey("CartId")]
        public virtual Cart? Cart { get; set; }
        
        public virtual ICollection<TableOrderItem> Items { get; set; } = new List<TableOrderItem>();
    }

    /// <summary>
    /// Masa sipariş öğeleri - Cart'tan bağımsız kalıcı saklama
    /// </summary>
    [Table("table_order_items")]
    public class TableOrderItem : BaseEntity
    {
        [Required]
        public string TableOrderId { get; set; } = string.Empty;

        [Required]
        public Guid ProductId { get; set; }

        [Required]
        [MaxLength(200)]
        public string ProductName { get; set; } = string.Empty; // Ürün adı değişse bile kayıt kalsın

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Required]
        [MaxLength(20)]
        public string TaxType { get; set; } = string.Empty; // standard, reduced, special

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; }

        [Required]
        public ItemStatus Status { get; set; } // pending, preparing, ready, served

        // RKSV uyumlu izleme
        public DateTime? PreparedTime { get; set; }
        public DateTime? ServedTime { get; set; }

        // Navigation properties
        [ForeignKey("TableOrderId")]
        public virtual TableOrder TableOrder { get; set; } = null!;
        
        // Product navigation property removed to prevent shadow property conflicts
    }

    public enum TableOrderStatus
    {
        Active = 1,        // Aktif sipariş (devam ediyor)
        Preparing = 2,     // Hazırlanıyor
        Ready = 3,         // Hazır
        Served = 4,        // Servis edildi
        Completed = 5,     // Tamamlandı (ödendi)
        Cancelled = 6,     // İptal edildi
        OnHold = 7         // Beklemede
    }

    public enum ItemStatus
    {
        Pending = 1,       // Bekliyor
        Preparing = 2,     // Hazırlanıyor
        Ready = 3,         // Hazır
        Served = 4,        // Servis edildi
        Cancelled = 5      // İptal edildi
    }

    // API Response modelleri
    public class TableOrderResponse
    {
        public string TableOrderId { get; set; } = string.Empty;
        public int TableNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? WaiterName { get; set; }
        public TableOrderStatus Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderStartTime { get; set; }
        public DateTime? LastModifiedTime { get; set; }
        public List<TableOrderItemResponse> Items { get; set; } = new List<TableOrderItemResponse>();
    }

    public class TableOrderItemResponse
    {
        public Guid Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string? Notes { get; set; }
        public ItemStatus Status { get; set; }
    }

    // Audit trail için status change modeli
    public class TableOrderStatusChange
    {
        public DateTime Timestamp { get; set; }
        public TableOrderStatus FromStatus { get; set; }
        public TableOrderStatus ToStatus { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}
