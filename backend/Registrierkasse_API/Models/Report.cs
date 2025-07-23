using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    // Rapor türleri enum'u
    public enum ReportType
    {
        SalesReport = 1,           // Satış raporu
        ProductReport = 2,         // Ürün raporu
        CategoryReport = 3,        // Kategori raporu
        PaymentReport = 4,         // Ödeme raporu
        InventoryReport = 5,       // Stok raporu
        UserActivityReport = 6,    // Kullanıcı aktivite raporu
        TaxReport = 7,            // Vergi raporu
        DailySummary = 8,         // Günlük özet
        WeeklySummary = 9,        // Haftalık özet
        MonthlySummary = 10       // Aylık özet
    }

    // Rapor erişim seviyeleri
    public enum ReportAccessLevel
    {
        Cashier = 1,      // Kasiyer - Temel raporlar
        Manager = 2,      // Yönetici - Detaylı raporlar
        Administrator = 3 // Admin - Tüm raporlar
    }

    // Rapor kayıt modeli
    [Table("reports")]
    public class Report : BaseEntity
    {
        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("report_type")]
        public ReportType ReportType { get; set; }

        [Required]
        [Column("access_level")]
        public ReportAccessLevel AccessLevel { get; set; }

        [Column("description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Column("parameters")]
        public string? Parameters { get; set; } // JSON formatında filtre parametreleri

        [Column("generated_by")]
        public Guid GeneratedBy { get; set; }

        [Column("generated_at")]
        public DateTime GeneratedAt { get; set; }

        [Column("file_path")]
        [MaxLength(500)]
        public string? FilePath { get; set; } // PDF/Excel dosya yolu

        [Column("is_scheduled")]
        public bool IsScheduled { get; set; } = false;

        [Column("schedule_cron")]
        [MaxLength(100)]
        public string? ScheduleCron { get; set; } // Cron expression

        // Navigation properties
        public virtual ApplicationUser GeneratedByUser { get; set; } = null!;
    }

    // Rapor filtre modeli
    public class ReportFilter
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? CategoryId { get; set; }
        public string? ProductId { get; set; }
        public string? UserId { get; set; }
        public string? PaymentMethod { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public bool? IsActive { get; set; }
        public string? SearchTerm { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
        public string? SortBy { get; set; }
        public string? SortOrder { get; set; } // "asc" or "desc"
    }

    // Satış raporu DTO
    public class SalesReportDto
    {
        public DateTime Date { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal NetSales { get; set; }
        public int TotalItems { get; set; }
        public Dictionary<string, decimal> SalesByCategory { get; set; } = new();
        public Dictionary<string, int> TopProducts { get; set; } = new();
        public Dictionary<string, decimal> SalesByPaymentMethod { get; set; } = new();
        public Dictionary<string, int> SalesByHour { get; set; } = new();
    }

    // Ürün raporu DTO
    public class ProductReportDto
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int SoldQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AveragePrice { get; set; }
        public int CurrentStock { get; set; }
        public int MinStockLevel { get; set; }
        public bool IsLowStock { get; set; }
        public decimal ProfitMargin { get; set; }
        public DateTime LastSold { get; set; }
    }

    // Kategori raporu DTO
    public class CategoryReportDto
    {
        public string CategoryId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public int SoldQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal MarketShare { get; set; } // Yüzde olarak
        public List<ProductReportDto> TopProducts { get; set; } = new();
    }

    // Ödeme raporu DTO
    public class PaymentReportDto
    {
        public DateTime Date { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; }
        public decimal RefundAmount { get; set; }
        public int RefundCount { get; set; }
        public Dictionary<string, decimal> AmountByHour { get; set; } = new();
    }

    // Stok raporu DTO
    public class InventoryReportDto
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int MinStockLevel { get; set; }
        public int MaxStockLevel { get; set; }
        public bool IsLowStock { get; set; }
        public bool IsOutOfStock { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalValue { get; set; }
        public DateTime LastRestocked { get; set; }
        public int DaysSinceLastRestock { get; set; }
    }

    // Kullanıcı aktivite raporu DTO
    public class UserActivityReportDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int InvoiceCount { get; set; }
        public decimal TotalSales { get; set; }
        public int LoginCount { get; set; }
        public DateTime LastLogin { get; set; }
        public DateTime LastActivity { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, int> ActionsByType { get; set; } = new();
    }

    // Vergi raporu DTO
    public class TaxReportDto
    {
        public DateTime Date { get; set; }
        public string TaxType { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public int InvoiceCount { get; set; }
        public Dictionary<string, decimal> TaxByCategory { get; set; } = new();
    }

    // Günlük/Haftalık/Aylık özet DTO
    public class SummaryReportDto
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string PeriodType { get; set; } = string.Empty; // "daily", "weekly", "monthly"
        public int TotalInvoices { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal NetSales { get; set; }
        public int TotalItems { get; set; }
        public int UniqueCustomers { get; set; }
        public decimal AverageOrderValue { get; set; }
        public Dictionary<string, decimal> SalesByDay { get; set; } = new();
        public List<string> TopCategories { get; set; } = new();
        public List<string> TopProducts { get; set; } = new();
    }
} 