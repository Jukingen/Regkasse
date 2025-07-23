using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace Registrierkasse_API.Services
{
    public interface IReportService
    {
        Task<SalesReportDto> GenerateSalesReportAsync(ReportFilter filter, string userId);
        Task<List<ProductReportDto>> GenerateProductReportAsync(ReportFilter filter, string userId);
        Task<List<CategoryReportDto>> GenerateCategoryReportAsync(ReportFilter filter, string userId);
        Task<List<PaymentReportDto>> GeneratePaymentReportAsync(ReportFilter filter, string userId);
        Task<List<InventoryReportDto>> GenerateInventoryReportAsync(ReportFilter filter, string userId);
        Task<List<UserActivityReportDto>> GenerateUserActivityReportAsync(ReportFilter filter, string userId);
        Task<List<TaxReportDto>> GenerateTaxReportAsync(ReportFilter filter, string userId);
        Task<SummaryReportDto> GenerateSummaryReportAsync(ReportFilter filter, string userId, string periodType);
        Task<List<Report>> GetUserReportsAsync(string userId, ReportAccessLevel accessLevel);
        Task<Report> SaveReportAsync(Report report);
        Task<bool> DeleteReportAsync(Guid reportId, string userId);
    }

    public class ReportService : IReportService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReportService> _logger;

        public ReportService(AppDbContext context, ILogger<ReportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SalesReportDto> GenerateSalesReportAsync(ReportFilter filter, string userId)
        {
            try
            {
                var query = _context.Invoices.AsQueryable();

                // Filtreleri uygula
                query = ApplyDateFilter(query, filter.StartDate, filter.EndDate);
                query = ApplyAmountFilter(query, filter.MinAmount, filter.MaxAmount);

                var salesData = await query
                    .Include(i => i.Items)
                    .ThenInclude(item => item.Product)
                    .ToListAsync();

                var report = new SalesReportDto
                {
                    Date = DateTime.Today,
                    InvoiceCount = salesData.Count,
                    TotalSales = salesData.Sum(i => i.TotalAmount),
                    TotalTax = salesData.Sum(i => i.TaxAmount),
                    TotalDiscount = 0, // Not available on Invoice
                    NetSales = salesData.Sum(i => i.TotalAmount - i.TaxAmount),
                    TotalItems = salesData.Sum(i => i.Items.Sum(item => item.Quantity)),
                    SalesByCategory = new Dictionary<string, decimal>(), // Not available
                    TopProducts = salesData
                        .SelectMany(i => i.Items)
                        .GroupBy(item => item.ProductName)
                        .OrderByDescending(g => g.Sum(item => item.Quantity))
                        .Take(10)
                        .ToDictionary(g => g.Key, g => g.Sum(item => item.Quantity)),
                    SalesByPaymentMethod = salesData
                        .GroupBy(i => i.PaymentMethod.HasValue ? i.PaymentMethod.ToString() : "Unknown")
                        .ToDictionary(g => g.Key, g => g.Sum(i => i.TotalAmount)),
                    SalesByHour = salesData
                        .GroupBy(i => i.CreatedAt.Hour)
                        .ToDictionary(g => g.Key.ToString(), g => g.Count())
                };

                _logger.LogInformation($"Sales report generated for user: {userId}");
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating sales report for user: {userId}");
                throw;
            }
        }

        public async Task<List<ProductReportDto>> GenerateProductReportAsync(ReportFilter filter, string userId)
        {
            try
            {
                var query = _context.Products.AsQueryable();

                // Filtreleri uygula
                if (!string.IsNullOrEmpty(filter.CategoryId))
                {
                    var category = await _context.Categories.FindAsync(Guid.Parse(filter.CategoryId));
                    if (category != null)
                    {
                        query = query.Where(p => p.Category == category.Name);
                    }
                }

                if (filter.IsActive.HasValue)
                {
                    query = query.Where(p => p.IsActive == filter.IsActive.Value);
                }

                if (!string.IsNullOrEmpty(filter.SearchTerm))
                {
                    query = query.Where(p => p.Name.Contains(filter.SearchTerm) || p.Description.Contains(filter.SearchTerm));
                }

                var products = await query.ToListAsync();

                var productReports = new List<ProductReportDto>();

                foreach (var product in products)
                {
                    var soldItems = await _context.InvoiceItems
                        .Where(item => item.ProductId == product.Id)
                        .ToListAsync();

                    var report = new ProductReportDto
                    {
                        ProductId = product.Id.ToString(),
                        ProductName = product.Name,
                        Category = product.Category,
                        SoldQuantity = soldItems.Sum(item => item.Quantity),
                        TotalRevenue = soldItems.Sum(item => item.TotalAmount),
                        AveragePrice = soldItems.Any() ? soldItems.Average(item => item.UnitPrice) : product.Price,
                        CurrentStock = product.StockQuantity,
                        MinStockLevel = product.MinStockLevel,
                        IsLowStock = product.StockQuantity <= product.MinStockLevel,
                        ProfitMargin = CalculateProfitMargin(product, soldItems),
                        LastSold = soldItems.Any() ? soldItems.Max(item => item.CreatedAt) : DateTime.MinValue
                    };

                    productReports.Add(report);
                }

                // Sıralama
                if (!string.IsNullOrEmpty(filter.SortBy))
                {
                    productReports = filter.SortBy.ToLower() switch
                    {
                        "revenue" => filter.SortOrder == "desc" 
                            ? productReports.OrderByDescending(p => p.TotalRevenue).ToList()
                            : productReports.OrderBy(p => p.TotalRevenue).ToList(),
                        "quantity" => filter.SortOrder == "desc"
                            ? productReports.OrderByDescending(p => p.SoldQuantity).ToList()
                            : productReports.OrderBy(p => p.SoldQuantity).ToList(),
                        "stock" => filter.SortOrder == "desc"
                            ? productReports.OrderByDescending(p => p.CurrentStock).ToList()
                            : productReports.OrderBy(p => p.CurrentStock).ToList(),
                        _ => productReports.OrderByDescending(p => p.TotalRevenue).ToList()
                    };
                }

                _logger.LogInformation($"Product report generated for user: {userId}");
                return productReports;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating product report for user: {userId}");
                throw;
            }
        }

        public async Task<List<CategoryReportDto>> GenerateCategoryReportAsync(ReportFilter filter, string userId)
        {
            try
            {
                var categories = await _context.Categories
                    .Where(c => c.IsActive)
                    .ToListAsync();

                var categoryReports = new List<CategoryReportDto>();
                var totalRevenue = await _context.Invoices.SumAsync(i => i.TotalAmount);

                foreach (var category in categories)
                {
                    var products = await _context.Products
                        .Where(p => p.Category == category.Name && p.IsActive)
                        .ToListAsync();

                    var soldItems = await _context.InvoiceItems
                        .Where(item => products.Any(p => p.Id == item.ProductId))
                        .ToListAsync();

                    var report = new CategoryReportDto
                    {
                        CategoryId = category.Id.ToString(),
                        CategoryName = category.Name,
                        ProductCount = products.Count,
                        SoldQuantity = soldItems.Sum(item => item.Quantity),
                        TotalRevenue = soldItems.Sum(item => item.TotalAmount),
                        AveragePrice = soldItems.Any() ? soldItems.Average(item => item.UnitPrice) : 0,
                        MarketShare = totalRevenue > 0 ? (soldItems.Sum(item => item.TotalAmount) / totalRevenue) * 100 : 0,
                        TopProducts = await GetTopProductsForCategory(category.Name, 5)
                    };

                    categoryReports.Add(report);
                }

                _logger.LogInformation($"Category report generated for user: {userId}");
                return categoryReports;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating category report for user: {userId}");
                throw;
            }
        }

        public async Task<List<PaymentReportDto>> GeneratePaymentReportAsync(ReportFilter filter, string userId)
        {
            try
            {
                var query = _context.Invoices.AsQueryable();

                // Filtreleri uygula
                query = ApplyDateFilter(query, filter.StartDate, filter.EndDate);
                query = ApplyAmountFilter(query, filter.MinAmount, filter.MaxAmount);

                if (!string.IsNullOrEmpty(filter.PaymentMethod))
                {
                    query = query.Where(i => i.PaymentMethod.HasValue && i.PaymentMethod.ToString() == filter.PaymentMethod);
                }

                var invoices = await query.ToListAsync();

                var paymentReports = invoices
                    .GroupBy(i => new { PaymentMethod = i.PaymentMethod.HasValue ? i.PaymentMethod.ToString() : "Unknown", Date = i.CreatedAt.Date })
                    .Select(g => new PaymentReportDto
                    {
                        Date = g.Key.Date,
                        PaymentMethod = g.Key.PaymentMethod,
                        TransactionCount = g.Count(),
                        TotalAmount = g.Sum(i => i.TotalAmount),
                        AverageAmount = g.Average(i => i.TotalAmount),
                        RefundAmount = 0, // Not available
                        RefundCount = 0, // Not available
                        AmountByHour = g.GroupBy(i => i.CreatedAt.Hour)
                            .ToDictionary(h => h.Key.ToString(), h => h.Sum(i => i.TotalAmount))
                    })
                    .ToList();

                _logger.LogInformation($"Payment report generated for user: {userId}");
                return paymentReports;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating payment report for user: {userId}");
                throw;
            }
        }

        public async Task<List<InventoryReportDto>> GenerateInventoryReportAsync(ReportFilter filter, string userId)
        {
            try
            {
                var query = _context.Products.AsQueryable();

                // Filtreleri uygula
                if (!string.IsNullOrEmpty(filter.CategoryId))
                {
                    var category = await _context.Categories.FindAsync(Guid.Parse(filter.CategoryId));
                    if (category != null)
                    {
                        query = query.Where(p => p.Category == category.Name);
                    }
                }

                if (filter.IsActive.HasValue)
                {
                    query = query.Where(p => p.IsActive == filter.IsActive.Value);
                }

                var products = await query.ToListAsync();

                var inventoryReports = products.Select(p => new InventoryReportDto
                {
                    ProductId = p.Id.ToString(),
                    ProductName = p.Name,
                    Category = p.Category,
                    CurrentStock = p.StockQuantity,
                    MinStockLevel = p.MinStockLevel,
                    MaxStockLevel = p.MinStockLevel * 3, // Varsayılan maksimum stok
                    IsLowStock = p.StockQuantity <= p.MinStockLevel,
                    IsOutOfStock = p.StockQuantity == 0,
                    UnitCost = p.Cost,
                    TotalValue = p.Cost * p.StockQuantity,
                    LastRestocked = p.UpdatedAt ?? p.CreatedAt,
                    DaysSinceLastRestock = (int)(DateTime.UtcNow - (p.UpdatedAt ?? p.CreatedAt)).TotalDays
                }).ToList();

                _logger.LogInformation($"Inventory report generated for user: {userId}");
                return inventoryReports;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating inventory report for user: {userId}");
                throw;
            }
        }

        public async Task<List<UserActivityReportDto>> GenerateUserActivityReportAsync(ReportFilter filter, string userId)
        {
            try
            {
                var query = _context.Users.AsQueryable();

                if (!string.IsNullOrEmpty(filter.UserId))
                {
                    query = query.Where(u => u.Id.ToString() == filter.UserId);
                }

                var users = await query.ToListAsync();
                var userReports = new List<UserActivityReportDto>();

                foreach (var user in users)
                {
                    var userInvoices = await _context.Invoices
                        .Where(i => i.CreatedById == user.Id.ToString())
                        .ToListAsync();

                    var report = new UserActivityReportDto
                    {
                        UserId = user.Id.ToString(),
                        UserName = user.UserName,
                        Role = user.Role,
                        InvoiceCount = userInvoices.Count,
                        TotalSales = userInvoices.Sum(i => i.TotalAmount),
                        LoginCount = 0, // Bu bilgi ayrı bir tabloda tutulmalı
                        LastLogin = user.LastLoginAt ?? DateTime.MinValue,
                        LastActivity = userInvoices.Any() ? userInvoices.Max(i => i.CreatedAt) : DateTime.MinValue,
                        IsActive = user.IsActive,
                        ActionsByType = new Dictionary<string, int>() // Bu bilgi ayrı bir tabloda tutulmalı
                    };

                    userReports.Add(report);
                }

                _logger.LogInformation($"User activity report generated for user: {userId}");
                return userReports;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating user activity report for user: {userId}");
                throw;
            }
        }

        public async Task<List<TaxReportDto>> GenerateTaxReportAsync(ReportFilter filter, string userId)
        {
            try
            {
                var query = _context.Invoices.AsQueryable();

                // Filtreleri uygula
                query = ApplyDateFilter(query, filter.StartDate, filter.EndDate);

                var invoices = await query.ToListAsync();

                var taxReports = invoices
                    .GroupBy(i => new { Date = i.CreatedAt.Date })
                    .Select(g => new TaxReportDto
                    {
                        Date = g.Key.Date,
                        TaxType = "", // Not available on Invoice
                        TaxRate = 0, // Not available
                        TaxableAmount = g.Sum(i => i.TotalAmount - i.TaxAmount),
                        TaxAmount = g.Sum(i => i.TaxAmount),
                        InvoiceCount = g.Count(),
                        TaxByCategory = new Dictionary<string, decimal>() // Not available
                    })
                    .ToList();

                _logger.LogInformation($"Tax report generated for user: {userId}");
                return taxReports;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating tax report for user: {userId}");
                throw;
            }
        }

        public async Task<SummaryReportDto> GenerateSummaryReportAsync(ReportFilter filter, string userId, string periodType)
        {
            try
            {
                var query = _context.Invoices.AsQueryable();

                // Filtreleri uygula
                query = ApplyDateFilter(query, filter.StartDate, filter.EndDate);

                var invoices = await query
                    .Include(i => i.Items)
                    .ThenInclude(item => item.Product)
                    .ToListAsync();

                var report = new SummaryReportDto
                {
                    PeriodStart = filter.StartDate ?? DateTime.Today.AddDays(-30),
                    PeriodEnd = filter.EndDate ?? DateTime.Today,
                    PeriodType = periodType,
                    TotalInvoices = invoices.Count,
                    TotalSales = invoices.Sum(i => i.TotalAmount),
                    TotalTax = invoices.Sum(i => i.TaxAmount),
                    TotalDiscount = 0, // Not available
                    NetSales = invoices.Sum(i => i.TotalAmount - i.TaxAmount),
                    TotalItems = invoices.Sum(i => i.Items.Sum(item => item.Quantity)),
                    UniqueCustomers = invoices.Select(i => i.CustomerId).Distinct().Count(),
                    AverageOrderValue = invoices.Any() ? invoices.Average(i => i.TotalAmount) : 0,
                    SalesByDay = invoices
                        .GroupBy(i => i.CreatedAt.Date)
                        .ToDictionary(g => g.Key.ToString("yyyy-MM-dd"), g => g.Sum(i => i.TotalAmount)),
                    TopCategories = new List<string>(), // Not available
                    TopProducts = invoices
                        .SelectMany(i => i.Items)
                        .GroupBy(item => item.ProductName)
                        .OrderByDescending(g => g.Sum(item => item.Quantity))
                        .Take(10)
                        .Select(g => g.Key)
                        .ToList()
                };

                _logger.LogInformation($"Summary report generated for user: {userId}");
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating summary report for user: {userId}");
                throw;
            }
        }

        public async Task<List<Report>> GetUserReportsAsync(string userId, ReportAccessLevel accessLevel)
        {
            try
            {
                var reports = await _context.Reports
                    .Where(r => r.AccessLevel <= accessLevel)
                    .OrderByDescending(r => r.GeneratedAt)
                    .ToListAsync();

                return reports;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting reports for user: {userId}");
                throw;
            }
        }

        public async Task<Report> SaveReportAsync(Report report)
        {
            try
            {
                _context.Reports.Add(report);
                await _context.SaveChangesAsync();
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving report");
                throw;
            }
        }

        public async Task<bool> DeleteReportAsync(Guid reportId, string userId)
        {
            try
            {
                var report = await _context.Reports.FindAsync(reportId);
                if (report == null) return false;

                _context.Reports.Remove(report);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting report: {reportId}");
                throw;
            }
        }

        // Yardımcı metodlar
        private IQueryable<Invoice> ApplyDateFilter(IQueryable<Invoice> query, DateTime? startDate, DateTime? endDate)
        {
            if (startDate.HasValue)
            {
                query = query.Where(i => i.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(i => i.CreatedAt <= endDate.Value.AddDays(1));
            }

            return query;
        }

        private IQueryable<Invoice> ApplyAmountFilter(IQueryable<Invoice> query, decimal? minAmount, decimal? maxAmount)
        {
            if (minAmount.HasValue)
            {
                query = query.Where(i => i.TotalAmount >= minAmount.Value);
            }

            if (maxAmount.HasValue)
            {
                query = query.Where(i => i.TotalAmount <= maxAmount.Value);
            }

            return query;
        }

        private decimal CalculateProfitMargin(Product product, List<InvoiceItem> soldItems)
        {
            if (!soldItems.Any()) return 0;

            var totalRevenue = soldItems.Sum(item => item.TotalAmount);
            var totalCost = soldItems.Sum(item => item.Quantity * product.Cost);

            return totalRevenue > 0 ? ((totalRevenue - totalCost) / totalRevenue) * 100 : 0;
        }

        private async Task<List<ProductReportDto>> GetTopProductsForCategory(string categoryName, int count)
        {
            var products = await _context.Products
                .Where(p => p.Category == categoryName && p.IsActive)
                .ToListAsync();

            var productReports = new List<ProductReportDto>();

            foreach (var product in products.Take(count))
            {
                var soldItems = await _context.InvoiceItems
                    .Where(item => item.ProductId == product.Id)
                    .ToListAsync();

                var report = new ProductReportDto
                {
                    ProductId = product.Id.ToString(),
                    ProductName = product.Name,
                    Category = product.Category,
                    SoldQuantity = soldItems.Sum(item => item.Quantity),
                    TotalRevenue = soldItems.Sum(item => item.TotalAmount),
                    AveragePrice = soldItems.Any() ? soldItems.Average(item => item.UnitPrice) : product.Price,
                    CurrentStock = product.StockQuantity,
                    MinStockLevel = product.MinStockLevel,
                    IsLowStock = product.StockQuantity <= product.MinStockLevel,
                    ProfitMargin = CalculateProfitMargin(product, soldItems),
                    LastSold = soldItems.Any() ? soldItems.Max(item => item.CreatedAt) : DateTime.MinValue
                };

                productReports.Add(report);
            }

            return productReports.OrderByDescending(p => p.TotalRevenue).ToList();
        }

        private decimal GetTaxRate(TaxType taxType)
        {
            return taxType switch
            {
                TaxType.Standard => 20.0m,
                TaxType.Reduced => 10.0m,
                TaxType.Special => 13.0m,
                _ => 0.0m
            };
        }
    }
} 