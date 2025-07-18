using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;

namespace Registrierkasse_API.Services
{
    public interface IAdvancedReportService
    {
        Task<SalesAnalyticsReport> GetSalesAnalyticsAsync(DateTime startDate, DateTime endDate);
        Task<InventoryAnalyticsReport> GetInventoryAnalyticsAsync();
        Task<CustomerAnalyticsReport> GetCustomerAnalyticsAsync(DateTime startDate, DateTime endDate);
        Task<FinancialReport> GetFinancialReportAsync(DateTime startDate, DateTime endDate);
        Task<OperationalReport> GetOperationalReportAsync(DateTime startDate, DateTime endDate);
        Task<DashboardSummary> GetDashboardSummaryAsync();
        Task<byte[]> ExportReportAsync(string reportType, DateTime startDate, DateTime endDate, string format = "pdf");
    }

    public class AdvancedReportService : IAdvancedReportService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AdvancedReportService> _logger;

        public AdvancedReportService(AppDbContext context, ILogger<AdvancedReportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SalesAnalyticsReport> GetSalesAnalyticsAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var invoices = await _context.Invoices
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .Where(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate)
                    .ToListAsync();

                var dailySales = invoices
                    .GroupBy(i => i.InvoiceDate.Date)
                    .Select(g => new DailySalesData
                    {
                        Date = g.Key,
                        TotalSales = g.Sum(i => i.TotalAmount),
                        InvoiceCount = g.Count(),
                        AverageTicket = g.Average(i => i.TotalAmount)
                    })
                    .OrderBy(d => d.Date)
                    .ToList();

                var topProducts = invoices
                    .SelectMany(i => i.Items)
                    .GroupBy(ii => new { ii.ProductId, ProductName = ii.ProductName })
                    .Select(g => new TopProductData
                    {
                        ProductId = Guid.TryParse(g.Key.ProductId.ToString(), out var pid) ? pid : Guid.Empty,
                        ProductName = g.Key.ProductName,
                        TotalQuantity = g.Sum(ii => ii.Quantity),
                        TotalRevenue = g.Sum(ii => ii.TotalAmount),
                        AveragePrice = g.Average(ii => ii.UnitPrice)
                    })
                    .OrderByDescending(p => p.TotalRevenue)
                    .Take(10)
                    .ToList();

                return new SalesAnalyticsReport
                {
                    Period = $"{startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}",
                    TotalSales = invoices.Sum(i => i.TotalAmount),
                    TotalInvoices = invoices.Count,
                    AverageTicket = invoices.Any() ? invoices.Average(i => i.TotalAmount) : 0,
                    DailySales = dailySales,
                    TopProducts = topProducts,
                    GeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate sales analytics report");
                throw;
            }
        }

        public async Task<InventoryAnalyticsReport> GetInventoryAnalyticsAsync()
        {
            try
            {
                var products = await _context.Products
                    .Include(p => p.Inventory)
                    .ToListAsync();

                var lowStockProducts = products
                    .Where(p => p.Inventory.CurrentStock <= p.Inventory.MinimumStock)
                    .Select(p => new LowStockProduct
                    {
                        ProductId = p.Id,
                        ProductName = p.Name,
                        CurrentStock = p.Inventory.CurrentStock,
                        MinQuantity = p.Inventory.MinimumStock,
                        ReorderLevel = p.Inventory.MinimumStock * 1.5m
                    })
                    .ToList();

                return new InventoryAnalyticsReport
                {
                    TotalProducts = products.Count,
                    LowStockProducts = lowStockProducts,
                    TotalStockValue = products.Sum(p => p.Inventory.CurrentStock * p.Price),
                    GeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate inventory analytics report");
                throw;
            }
        }

        public async Task<CustomerAnalyticsReport> GetCustomerAnalyticsAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var invoices = await _context.Invoices
                    .Include(i => i.Customer)
                    .Where(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate)
                    .ToListAsync();

                var topCustomers = invoices
                    .GroupBy(i => new { i.CustomerId, CustomerName = i.Customer != null ? i.Customer.FirstName + " " + i.Customer.LastName : "" })
                    .Select(g => new TopCustomerData
                    {
                        CustomerId = Guid.TryParse(g.Key.CustomerId?.ToString(), out var cid) ? cid : Guid.Empty,
                        CustomerName = g.Key.CustomerName,
                        TotalSpent = g.Sum(i => i.TotalAmount),
                        InvoiceCount = g.Count(),
                        AverageOrder = g.Average(i => i.TotalAmount),
                        LastOrderDate = g.Max(i => i.InvoiceDate)
                    })
                    .OrderByDescending(c => c.TotalSpent)
                    .Take(10)
                    .ToList();

                return new CustomerAnalyticsReport
                {
                    Period = $"{startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}",
                    TotalCustomers = invoices.Select(i => i.CustomerId).Distinct().Count(),
                    TotalRevenue = invoices.Sum(i => i.TotalAmount),
                    TopCustomers = topCustomers,
                    GeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate customer analytics report");
                throw;
            }
        }

        public async Task<FinancialReport> GetFinancialReportAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var invoices = await _context.Invoices
                    .Where(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate)
                    .ToListAsync();

                var totalRevenue = invoices.Sum(i => i.TotalAmount);
                var totalTax = invoices.Sum(i => i.TaxAmount);
                var netRevenue = totalRevenue - totalTax;

                return new FinancialReport
                {
                    Period = $"{startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}",
                    TotalRevenue = totalRevenue,
                    TotalTax = totalTax,
                    NetRevenue = netRevenue,
                    GeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate financial report");
                throw;
            }
        }

        public async Task<OperationalReport> GetOperationalReportAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var invoices = await _context.Invoices
                    .Where(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate)
                    .ToListAsync();

                var dailyOperations = invoices
                    .GroupBy(i => i.InvoiceDate.Date)
                    .Select(g => new DailyOperationData
                    {
                        Date = g.Key,
                        InvoiceCount = g.Count(),
                        TotalSales = g.Sum(i => i.TotalAmount),
                        AverageItemsPerInvoice = g.Average(i => i.Items != null ? i.Items.Count : 0)
                    })
                    .OrderBy(d => d.Date)
                    .ToList();

                return new OperationalReport
                {
                    Period = $"{startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}",
                    TotalInvoices = invoices.Count,
                    TotalSales = invoices.Sum(i => i.TotalAmount),
                    DailyOperations = dailyOperations,
                    GeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate operational report");
                throw;
            }
        }

        public async Task<DashboardSummary> GetDashboardSummaryAsync()
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var thisMonth = new DateTime(today.Year, today.Month, 1);
                var lastMonth = thisMonth.AddMonths(-1);

                var todaySales = await _context.Invoices
                    .Where(i => i.InvoiceDate.Date == today)
                    .SumAsync(i => i.TotalAmount);

                var thisMonthSales = await _context.Invoices
                    .Where(i => i.InvoiceDate >= thisMonth)
                    .SumAsync(i => i.TotalAmount);

                var lastMonthSales = await _context.Invoices
                    .Where(i => i.InvoiceDate >= lastMonth && i.InvoiceDate < thisMonth)
                    .SumAsync(i => i.TotalAmount);

                var lowStockCount = await _context.Products
                    .Include(p => p.Inventory)
                    .Where(p => p.Inventory.CurrentStock <= p.Inventory.MinimumStock)
                    .CountAsync();

                var pendingInvoices = await _context.Invoices
                    .Where(i => i.PaymentStatus == Models.PaymentStatus.Pending)
                    .CountAsync();

                var totalCustomers = await _context.Customers.CountAsync();
                var totalProducts = await _context.Products.CountAsync();

                return new DashboardSummary
                {
                    TodaySales = todaySales,
                    ThisMonthSales = thisMonthSales,
                    LastMonthSales = lastMonthSales,
                    SalesGrowth = lastMonthSales > 0 ? (double)((thisMonthSales - lastMonthSales) / lastMonthSales * 100) : 0,
                    LowStockCount = lowStockCount,
                    PendingInvoices = pendingInvoices,
                    TotalCustomers = totalCustomers,
                    TotalProducts = totalProducts,
                    GeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate dashboard summary");
                throw;
            }
        }

        public async Task<byte[]> ExportReportAsync(string reportType, DateTime startDate, DateTime endDate, string format = "pdf")
        {
            try
            {
                object reportData;
                if (reportType == "sales")
                    reportData = await GetSalesAnalyticsAsync(startDate, endDate);
                else if (reportType == "inventory")
                    reportData = await GetInventoryAnalyticsAsync();
                else if (reportType == "customers")
                    reportData = await GetCustomerAnalyticsAsync(startDate, endDate);
                else if (reportType == "financial")
                    reportData = await GetFinancialReportAsync(startDate, endDate);
                else if (reportType == "operational")
                    reportData = await GetOperationalReportAsync(startDate, endDate);
                else
                    throw new ArgumentException($"Unknown report type: {reportType}");

                var jsonData = JsonSerializer.Serialize(reportData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                return System.Text.Encoding.UTF8.GetBytes(jsonData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export report: {ReportType}", reportType);
                throw;
            }
        }
    }

    // Report Models
    public class SalesAnalyticsReport
    {
        public string Period { get; set; } = string.Empty;
        public decimal TotalSales { get; set; }
        public int TotalInvoices { get; set; }
        public decimal AverageTicket { get; set; }
        public List<DailySalesData> DailySales { get; set; } = new();
        public List<TopProductData> TopProducts { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    public class DailySalesData
    {
        public DateTime Date { get; set; }
        public decimal TotalSales { get; set; }
        public int InvoiceCount { get; set; }
        public decimal AverageTicket { get; set; }
    }

    public class TopProductData
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AveragePrice { get; set; }
    }

    public class InventoryAnalyticsReport
    {
        public int TotalProducts { get; set; }
        public List<LowStockProduct> LowStockProducts { get; set; } = new();
        public decimal TotalStockValue { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class LowStockProduct
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal CurrentStock { get; set; }
        public decimal MinQuantity { get; set; }
        public decimal ReorderLevel { get; set; }
    }

    public class CustomerAnalyticsReport
    {
        public string Period { get; set; } = string.Empty;
        public int TotalCustomers { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<TopCustomerData> TopCustomers { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    public class TopCustomerData
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalSpent { get; set; }
        public int InvoiceCount { get; set; }
        public decimal AverageOrder { get; set; }
        public DateTime LastOrderDate { get; set; }
    }

    public class FinancialReport
    {
        public string Period { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public decimal TotalTax { get; set; }
        public decimal NetRevenue { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class OperationalReport
    {
        public string Period { get; set; } = string.Empty;
        public int TotalInvoices { get; set; }
        public decimal TotalSales { get; set; }
        public List<DailyOperationData> DailyOperations { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    public class DailyOperationData
    {
        public DateTime Date { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalSales { get; set; }
        public double AverageItemsPerInvoice { get; set; }
    }

    public class DashboardSummary
    {
        public decimal TodaySales { get; set; }
        public decimal ThisMonthSales { get; set; }
        public decimal LastMonthSales { get; set; }
        public double SalesGrowth { get; set; }
        public int LowStockCount { get; set; }
        public int PendingInvoices { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalProducts { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
} 
