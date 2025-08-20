using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(AppDbContext context, ILogger<ReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/reports/sales
        [HttpGet("sales")]
        public async Task<ActionResult<SalesReport>> GetSalesReport([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;

                var invoices = await _context.Invoices
                    .Where(i => i.InvoiceDate >= start && i.InvoiceDate <= end && i.IsActive)
                    .ToListAsync();

                var salesReport = new SalesReport
                {
                    StartDate = start,
                    EndDate = end,
                    TotalInvoices = invoices.Count,
                    TotalSales = invoices.Sum(i => i.TotalAmount),
                    TotalTax = invoices.Sum(i => i.TaxAmount),
                    AverageOrderValue = invoices.Any() ? invoices.Average(i => i.TotalAmount) : 0,
                    SalesByStatus = invoices.GroupBy(i => i.Status)
                        .Select(g => new SalesByStatus
                        {
                            Status = g.Key.ToString(),
                            Count = g.Count(),
                            Total = g.Sum(i => i.TotalAmount)
                        }).ToList(),
                    DailySales = invoices.GroupBy(i => i.InvoiceDate.Date)
                        .Select(g => new DailySales
                        {
                            Date = g.Key,
                            Count = g.Count(),
                            Total = g.Sum(i => i.TotalAmount)
                        })
                        .OrderBy(d => d.Date)
                        .ToList()
                };

                return Ok(salesReport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating sales report");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/reports/products
        [HttpGet("products")]
        public async Task<ActionResult<ProductReport>> GetProductReport([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;

                var orderItems = await _context.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => oi.Order.OrderDate >= start && oi.Order.OrderDate <= end && oi.Order.IsActive)
                    .ToListAsync();

                var productReport = new ProductReport
                {
                    StartDate = start,
                    EndDate = end,
                    TotalProductsSold = orderItems.Sum(oi => oi.Quantity),
                    TotalRevenue = orderItems.Sum(oi => oi.TotalAmount),
                    TopSellingProducts = orderItems.GroupBy(oi => new { oi.ProductId, oi.ProductName })
                        .Select(g => new TopSellingProduct
                        {
                            ProductId = g.Key.ProductId,
                            ProductName = g.Key.ProductName,
                            QuantitySold = g.Sum(oi => oi.Quantity),
                            Revenue = g.Sum(oi => oi.TotalAmount)
                        })
                        .OrderByDescending(p => p.QuantitySold)
                        .Take(10)
                        .ToList(),
                    ProductsByCategory = orderItems.GroupBy(oi => oi.ProductCategory)
                        .Select(g => new ProductsByCategory
                        {
                            Category = g.Key ?? "Uncategorized",
                            QuantitySold = g.Sum(oi => oi.Quantity),
                            Revenue = g.Sum(oi => oi.TotalAmount)
                        })
                        .OrderByDescending(c => c.Revenue)
                        .ToList()
                };

                return Ok(productReport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating product report");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/reports/customers
        [HttpGet("customers")]
        public async Task<ActionResult<CustomerReport>> GetCustomerReport([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;

                var invoices = await _context.Invoices
                    .Where(i => i.InvoiceDate >= start && i.InvoiceDate <= end && i.IsActive)
                    .ToListAsync();

                var customerReport = new CustomerReport
                {
                    StartDate = start,
                    EndDate = end,
                    TotalCustomers = invoices.Select(i => i.CustomerName).Where(n => !string.IsNullOrEmpty(n)).Distinct().Count(),
                    TotalOrders = invoices.Count,
                    AverageOrderValue = invoices.Any() ? invoices.Average(i => i.TotalAmount) : 0,
                    TopCustomers = invoices.GroupBy(i => i.CustomerName ?? "Unknown")
                        .Select(g => new TopCustomer
                        {
                            CustomerId = g.Key,
                            CustomerName = g.Key,
                            OrderCount = g.Count(),
                            TotalSpent = g.Sum(i => i.TotalAmount),
                            AverageOrderValue = g.Average(i => i.TotalAmount)
                        })
                        .OrderByDescending(c => c.TotalSpent)
                        .Take(10)
                        .ToList()
                };

                return Ok(customerReport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating customer report");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/reports/inventory
        [HttpGet("inventory")]
        public async Task<ActionResult<InventoryReport>> GetInventoryReport()
        {
            try
            {
                var products = await _context.Products
                    .Where(p => p.IsActive)
                    .ToListAsync();

                var inventoryReport = new InventoryReport
                {
                    TotalProducts = products.Count,
                    TotalValue = products.Sum(p => p.Price * p.StockQuantity),
                    LowStockProducts = products.Where(p => p.StockQuantity <= p.MinStockLevel)
                        .Select(p => new LowStockProduct
                        {
                            ProductId = p.Id,
                            ProductName = p.Name,
                            CurrentStock = p.StockQuantity,
                            MinStockLevel = p.MinStockLevel,
                            Category = p.Category
                        })
                        .OrderBy(p => p.CurrentStock)
                        .ToList(),
                    ProductsByCategory = products.GroupBy(p => p.Category)
                        .Select(g => new InventoryByCategory
                        {
                            Category = g.Key ?? "Uncategorized",
                            ProductCount = g.Count(),
                            TotalValue = g.Sum(p => p.Price * p.StockQuantity),
                            AverageStock = g.Average(p => p.StockQuantity)
                        })
                        .OrderByDescending(c => c.TotalValue)
                        .ToList()
                };

                return Ok(inventoryReport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating inventory report");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/reports/payments
        [HttpGet("payments")]
        public async Task<ActionResult<PaymentReport>> GetPaymentReport([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;

                var payments = await _context.PaymentDetails
                    .Where(p => p.CreatedAt >= start && p.CreatedAt <= end)
                    .ToListAsync();

                var paymentReport = new PaymentReport
                {
                    StartDate = start,
                    EndDate = end,
                    TotalPayments = payments.Count,
                    TotalAmount = payments.Sum(p => p.TotalAmount),
                    PaymentsByMethod = payments.GroupBy(p => p.PaymentMethod)
                        .Select(g => new PaymentsByMethod
                        {
                            Method = g.Key.ToString(),
                            Count = g.Count(),
                            Total = g.Sum(p => p.TotalAmount)
                        })
                        .OrderByDescending(p => p.Total)
                        .ToList(),
                    PaymentsByStatus = payments.GroupBy(p => p.IsActive)
                        .Select(g => new PaymentsByStatus
                        {
                            Status = g.Key ? "Active" : "Inactive",
                            Count = g.Count(),
                            Total = g.Sum(p => p.TotalAmount)
                        })
                        .OrderByDescending(p => p.Total)
                        .ToList()
                };

                return Ok(paymentReport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating payment report");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/reports/export/sales
        [HttpGet("export/sales")]
        public async Task<IActionResult> ExportSalesReport([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string format = "json")
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;

                var invoices = await _context.Invoices
                    .Where(i => i.InvoiceDate >= start && i.InvoiceDate <= end && i.IsActive)
                    .ToListAsync();

                if (format.ToLower() == "csv")
                {
                    var csv = GenerateSalesCsv(invoices);
                    var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                    return File(bytes, "text/csv", $"sales_report_{start:yyyyMMdd}_{end:yyyyMMdd}.csv");
                }

                return Ok(invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting sales report");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        private string GenerateSalesCsv(List<Invoice> invoices)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Invoice Number,Date,Customer,Total Amount,Tax Amount,Status");

            foreach (var invoice in invoices)
            {
                csv.AppendLine($"{invoice.InvoiceNumber},{invoice.InvoiceDate:yyyy-MM-dd},{invoice.CustomerName},{invoice.TotalAmount},{invoice.TaxAmount},{invoice.Status}");
            }

            return csv.ToString();
        }
    }

    // Report Models
    public class SalesReport
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalTax { get; set; }
        public decimal AverageOrderValue { get; set; }
        public List<SalesByStatus> SalesByStatus { get; set; } = new();
        public List<DailySales> DailySales { get; set; } = new();
    }

    public class SalesByStatus
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Total { get; set; }
    }

    public class DailySales
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public decimal Total { get; set; }
    }

    public class ProductReport
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalProductsSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<TopSellingProduct> TopSellingProducts { get; set; } = new();
        public List<ProductsByCategory> ProductsByCategory { get; set; } = new();
    }

    public class TopSellingProduct
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class ProductsByCategory
    {
        public string Category { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class CustomerReport
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public List<TopCustomer> TopCustomers { get; set; } = new();
    }

    public class TopCustomer
    {
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal AverageOrderValue { get; set; }
    }

    public class InventoryReport
    {
        public int TotalProducts { get; set; }
        public decimal TotalValue { get; set; }
        public List<LowStockProduct> LowStockProducts { get; set; } = new();
        public List<InventoryByCategory> ProductsByCategory { get; set; } = new();
    }

    public class LowStockProduct
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int MinStockLevel { get; set; }
        public string? Category { get; set; }
    }

    public class InventoryByCategory
    {
        public string Category { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public decimal TotalValue { get; set; }
        public double AverageStock { get; set; }
    }

    public class PaymentReport
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalPayments { get; set; }
        public decimal TotalAmount { get; set; }
        public List<PaymentsByMethod> PaymentsByMethod { get; set; } = new();
        public List<PaymentsByStatus> PaymentsByStatus { get; set; } = new();
    }

    public class PaymentsByMethod
    {
        public string Method { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Total { get; set; }
    }

    public class PaymentsByStatus
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Total { get; set; }
    }
}
