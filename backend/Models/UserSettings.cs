using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("UserSettings")]
    public class UserSettings
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        // Dil ve lokalizasyon ayarları
        [StringLength(10)]
        public string Language { get; set; } = "de-DE";

        [StringLength(3)]
        public string Currency { get; set; } = "EUR";

        [StringLength(20)]
        public string DateFormat { get; set; } = "DD.MM.YYYY";

        [StringLength(10)]
        public string TimeFormat { get; set; } = "24h";

        // Kasa konfigürasyonu
        [StringLength(100)]
        public string? CashRegisterId { get; set; }

        [Range(0, 100)]
        public int DefaultTaxRate { get; set; } = 20;

        public bool EnableDiscounts { get; set; } = true;

        public bool EnableCoupons { get; set; } = true;

        public bool AutoPrintReceipts { get; set; } = false;

        [StringLength(200)]
        public string? ReceiptHeader { get; set; }

        [StringLength(200)]
        public string? ReceiptFooter { get; set; }

        // TSE ve FinanzOnline ayarları
        [StringLength(100)]
        public string? TseDeviceId { get; set; }

        public bool FinanzOnlineEnabled { get; set; } = false;

        [StringLength(100)]
        public string? FinanzOnlineUsername { get; set; }

        // Güvenlik ayarları
        [Range(1, 480)] // 1 dakika - 8 saat
        public int SessionTimeout { get; set; } = 30;

        public bool RequirePinForRefunds { get; set; } = true;

        [Range(0, 100)]
        public int MaxDiscountPercentage { get; set; } = 50;

        // Görünüm ayarları
        [StringLength(10)]
        public string Theme { get; set; } = "light";

        public bool CompactMode { get; set; } = false;

        public bool ShowProductImages { get; set; } = true;

        // Bildirim ayarları
        public bool EnableNotifications { get; set; } = true;

        public bool LowStockAlert { get; set; } = true;

        [StringLength(255)]
        public string? DailyReportEmail { get; set; }

        // Varsayılan değerler
        [StringLength(20)]
        public string DefaultPaymentMethod { get; set; } = "mixed";

        [StringLength(10)]
        public string? DefaultTableNumber { get; set; }

        [StringLength(100)]
        public string? DefaultWaiterName { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
    }
}
