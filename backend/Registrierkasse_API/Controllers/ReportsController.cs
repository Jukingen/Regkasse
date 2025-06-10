using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse.Services;

namespace Registrierkasse.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly IAdvancedReportService _reportService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(IAdvancedReportService reportService, ILogger<ReportsController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        [HttpGet("dashboard-summary")]
        public async Task<IActionResult> GetDashboardSummary()
        {
            try
            {
                var summary = await _reportService.GetDashboardSummaryAsync();
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate dashboard summary");
                return StatusCode(500, new { error = "Failed to generate dashboard summary" });
            }
        }

        [HttpGet("sales-analytics")]
        public async Task<IActionResult> GetSalesAnalytics(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                var analytics = await _reportService.GetSalesAnalyticsAsync(startDate, endDate);
                return Ok(analytics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate sales analytics");
                return StatusCode(500, new { error = "Failed to generate sales analytics" });
            }
        }

        [HttpGet("inventory-analytics")]
        public async Task<IActionResult> GetInventoryAnalytics()
        {
            try
            {
                var analytics = await _reportService.GetInventoryAnalyticsAsync();
                return Ok(analytics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate inventory analytics");
                return StatusCode(500, new { error = "Failed to generate inventory analytics" });
            }
        }

        [HttpGet("customer-analytics")]
        public async Task<IActionResult> GetCustomerAnalytics(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                var analytics = await _reportService.GetCustomerAnalyticsAsync(startDate, endDate);
                return Ok(analytics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate customer analytics");
                return StatusCode(500, new { error = "Failed to generate customer analytics" });
            }
        }

        [HttpGet("financial-report")]
        public async Task<IActionResult> GetFinancialReport(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                var report = await _reportService.GetFinancialReportAsync(startDate, endDate);
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate financial report");
                return StatusCode(500, new { error = "Failed to generate financial report" });
            }
        }

        [HttpGet("operational-report")]
        public async Task<IActionResult> GetOperationalReport(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                var report = await _reportService.GetOperationalReportAsync(startDate, endDate);
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate operational report");
                return StatusCode(500, new { error = "Failed to generate operational report" });
            }
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportReport(
            [FromQuery] string reportType,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] string format = "pdf")
        {
            try
            {
                var reportData = await _reportService.ExportReportAsync(reportType, startDate, endDate, format);
                
                var fileName = $"{reportType}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.{format}";
                string contentType;
                if (format.ToLower() == "pdf")
                    contentType = "application/pdf";
                else if (format.ToLower() == "excel")
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                else if (format.ToLower() == "csv")
                    contentType = "text/csv";
                else if (format.ToLower() == "json")
                    contentType = "application/json";
                else
                    contentType = "application/octet-stream";

                return File(reportData, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export report: {ReportType}", reportType);
                return StatusCode(500, new { error = "Failed to export report" });
            }
        }

        [HttpGet("top-products")]
        public async Task<IActionResult> GetTopProducts(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int limit = 10)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;
                
                var analytics = await _reportService.GetSalesAnalyticsAsync(start, end);
                var topProducts = analytics.TopProducts.Take(limit);
                
                return Ok(new { topProducts });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get top products");
                return StatusCode(500, new { error = "Failed to get top products" });
            }
        }

        [HttpGet("payment-methods")]
        public async Task<IActionResult> GetPaymentMethods(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;
                
                var analytics = await _reportService.GetSalesAnalyticsAsync(start, end);
                
                // This would need to be implemented in the service
                var paymentMethods = new[]
                {
                    new { method = "Cash", amount = 1500.00m, percentage = 60.0 },
                    new { method = "Card", amount = 800.00m, percentage = 32.0 },
                    new { method = "Voucher", amount = 200.00m, percentage = 8.0 }
                };
                
                return Ok(new { paymentMethodBreakdown = paymentMethods });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get payment methods");
                return StatusCode(500, new { error = "Failed to get payment methods" });
            }
        }

        [HttpGet("low-stock-alert")]
        public async Task<IActionResult> GetLowStockAlert()
        {
            try
            {
                var analytics = await _reportService.GetInventoryAnalyticsAsync();
                return Ok(new { lowStockProducts = analytics.LowStockProducts });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get low stock alert");
                return StatusCode(500, new { error = "Failed to get low stock alert" });
            }
        }

        [HttpGet("revenue-trend")]
        public async Task<IActionResult> GetRevenueTrend(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] string period = "daily") // daily, weekly, monthly
        {
            try
            {
                var analytics = await _reportService.GetSalesAnalyticsAsync(startDate, endDate);
                
                IEnumerable<object> trendData;
                if (period.ToLower() == "daily")
                {
                    trendData = analytics.DailySales.Select(d => new { date = d.Date, revenue = d.TotalSales });
                }
                else if (period.ToLower() == "weekly")
                {
                    trendData = analytics.DailySales
                        .GroupBy(d => System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(d.Date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday))
                        .Select(g => new { week = g.Key, revenue = g.Sum(d => d.TotalSales) });
                }
                else if (period.ToLower() == "monthly")
                {
                    trendData = analytics.DailySales
                        .GroupBy(d => new { d.Date.Year, d.Date.Month })
                        .Select(g => new { month = $"{g.Key.Year}-{g.Key.Month:D2}", revenue = g.Sum(d => d.TotalSales) });
                }
                else
                {
                    trendData = analytics.DailySales.Select(d => new { date = d.Date, revenue = d.TotalSales });
                }
                
                return Ok(new { trendData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get revenue trend");
                return StatusCode(500, new { error = "Failed to get revenue trend" });
            }
        }
    }
} 