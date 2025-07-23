using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Registrierkasse_API.Models;
using Registrierkasse_API.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Linq;

namespace Registrierkasse_API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(IReportService reportService, ILogger<ReportsController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        // Kasiyer erişimi - Temel raporlar
        [HttpGet("sales")]
        [Authorize(Roles = "Cashier,Manager,Administrator")]
        public async Task<ActionResult<SalesReportDto>> GetSalesReport([FromQuery] ReportFilterRequest filter)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var reportFilter = MapToReportFilter(filter);
                var report = await _reportService.GenerateSalesReportAsync(reportFilter, userId);

                _logger.LogInformation($"Sales report requested by user: {userId}");
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating sales report");
                return StatusCode(500, new { error = "Satış raporu oluşturulurken hata oluştu" });
            }
        }

        [HttpGet("products")]
        [Authorize(Roles = "Cashier,Manager,Administrator")]
        public async Task<ActionResult<List<ProductReportDto>>> GetProductReport([FromQuery] ReportFilterRequest filter)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var reportFilter = MapToReportFilter(filter);
                var report = await _reportService.GenerateProductReportAsync(reportFilter, userId);

                _logger.LogInformation($"Product report requested by user: {userId}");
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating product report");
                return StatusCode(500, new { error = "Ürün raporu oluşturulurken hata oluştu" });
            }
        }

        [HttpGet("categories")]
        [Authorize(Roles = "Cashier,Manager,Administrator")]
        public async Task<ActionResult<List<CategoryReportDto>>> GetCategoryReport([FromQuery] ReportFilterRequest filter)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var reportFilter = MapToReportFilter(filter);
                var report = await _reportService.GenerateCategoryReportAsync(reportFilter, userId);

                _logger.LogInformation($"Category report requested by user: {userId}");
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating category report");
                return StatusCode(500, new { error = "Kategori raporu oluşturulurken hata oluştu" });
            }
        }

        [HttpGet("payments")]
        [Authorize(Roles = "Cashier,Manager,Administrator")]
        public async Task<ActionResult<List<PaymentReportDto>>> GetPaymentReport([FromQuery] ReportFilterRequest filter)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var reportFilter = MapToReportFilter(filter);
                var report = await _reportService.GeneratePaymentReportAsync(reportFilter, userId);

                _logger.LogInformation($"Payment report requested by user: {userId}");
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating payment report");
                return StatusCode(500, new { error = "Ödeme raporu oluşturulurken hata oluştu" });
            }
        }

        // Yönetici erişimi - Detaylı raporlar
        [HttpGet("inventory")]
        [Authorize(Roles = "Manager,Administrator")]
        public async Task<ActionResult<List<InventoryReportDto>>> GetInventoryReport([FromQuery] ReportFilterRequest filter)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var reportFilter = MapToReportFilter(filter);
                var report = await _reportService.GenerateInventoryReportAsync(reportFilter, userId);

                _logger.LogInformation($"Inventory report requested by user: {userId}");
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating inventory report");
                return StatusCode(500, new { error = "Stok raporu oluşturulurken hata oluştu" });
            }
        }

        [HttpGet("tax")]
        [Authorize(Roles = "Manager,Administrator")]
        public async Task<ActionResult<List<TaxReportDto>>> GetTaxReport([FromQuery] ReportFilterRequest filter)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var reportFilter = MapToReportFilter(filter);
                var report = await _reportService.GenerateTaxReportAsync(reportFilter, userId);

                _logger.LogInformation($"Tax report requested by user: {userId}");
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating tax report");
                return StatusCode(500, new { error = "Vergi raporu oluşturulurken hata oluştu" });
            }
        }

        [HttpGet("summary")]
        [Authorize(Roles = "Manager,Administrator")]
        public async Task<ActionResult<SummaryReportDto>> GetSummaryReport([FromQuery] ReportFilterRequest filter, [FromQuery] string periodType = "daily")
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var reportFilter = MapToReportFilter(filter);
                var report = await _reportService.GenerateSummaryReportAsync(reportFilter, userId, periodType);

                _logger.LogInformation($"Summary report requested by user: {userId}");
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating summary report");
                return StatusCode(500, new { error = "Özet rapor oluşturulurken hata oluştu" });
            }
        }

        // Admin erişimi - Tüm raporlar
        [HttpGet("user-activity")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<List<UserActivityReportDto>>> GetUserActivityReport([FromQuery] ReportFilterRequest filter)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var reportFilter = MapToReportFilter(filter);
                var report = await _reportService.GenerateUserActivityReportAsync(reportFilter, userId);

                _logger.LogInformation($"User activity report requested by user: {userId}");
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating user activity report");
                return StatusCode(500, new { error = "Kullanıcı aktivite raporu oluşturulurken hata oluştu" });
            }
        }

        // Rapor yönetimi
        [HttpGet("saved")]
        public async Task<ActionResult<List<ReportDto>>> GetSavedReports()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var accessLevel = GetAccessLevelFromRole(userRole);

                var reports = await _reportService.GetUserReportsAsync(userId, accessLevel);
                var reportDtos = reports.Select(r => new ReportDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    ReportType = r.ReportType.ToString(),
                    AccessLevel = r.AccessLevel.ToString(),
                    Description = r.Description,
                    GeneratedAt = r.GeneratedAt,
                    GeneratedBy = r.GeneratedBy.ToString(),
                    FilePath = r.FilePath
                }).ToList();

                return Ok(reportDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved reports");
                return StatusCode(500, new { error = "Kaydedilmiş raporlar yüklenirken hata oluştu" });
            }
        }

        [HttpPost("save")]
        public async Task<ActionResult<ReportDto>> SaveReport([FromBody] SaveReportRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var report = new Report
                {
                    Name = request.Name,
                    ReportType = request.ReportType,
                    AccessLevel = request.AccessLevel,
                    Description = request.Description,
                    Parameters = request.Parameters,
                    GeneratedBy = Guid.Parse(userId),
                    GeneratedAt = DateTime.UtcNow,
                    IsScheduled = request.IsScheduled,
                    ScheduleCron = request.ScheduleCron
                };

                var savedReport = await _reportService.SaveReportAsync(report);

                var reportDto = new ReportDto
                {
                    Id = savedReport.Id,
                    Name = savedReport.Name,
                    ReportType = savedReport.ReportType.ToString(),
                    AccessLevel = savedReport.AccessLevel.ToString(),
                    Description = savedReport.Description,
                    GeneratedAt = savedReport.GeneratedAt,
                    GeneratedBy = savedReport.GeneratedBy.ToString(),
                    FilePath = savedReport.FilePath
                };

                _logger.LogInformation($"Report saved by user: {userId}");
                return Ok(reportDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving report");
                return StatusCode(500, new { error = "Rapor kaydedilirken hata oluştu" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReport(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var success = await _reportService.DeleteReportAsync(id, userId);
                if (!success)
                {
                    return NotFound(new { error = "Rapor bulunamadı" });
                }

                _logger.LogInformation($"Report deleted by user: {userId}");
                return Ok(new { message = "Rapor başarıyla silindi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting report");
                return StatusCode(500, new { error = "Rapor silinirken hata oluştu" });
            }
        }

        // Yardımcı metodlar
        private ReportFilter MapToReportFilter(ReportFilterRequest request)
        {
            return new ReportFilter
            {
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                CategoryId = request.CategoryId,
                ProductId = request.ProductId,
                UserId = request.UserId,
                PaymentMethod = request.PaymentMethod,
                MinAmount = request.MinAmount,
                MaxAmount = request.MaxAmount,
                IsActive = request.IsActive,
                SearchTerm = request.SearchTerm,
                Page = request.Page,
                PageSize = request.PageSize,
                SortBy = request.SortBy,
                SortOrder = request.SortOrder
            };
        }

        private ReportAccessLevel GetAccessLevelFromRole(string? role)
        {
            return role?.ToLower() switch
            {
                "administrator" => ReportAccessLevel.Administrator,
                "manager" => ReportAccessLevel.Manager,
                "cashier" => ReportAccessLevel.Cashier,
                _ => ReportAccessLevel.Cashier
            };
        }
    }

    // Request/Response DTOs
    public class ReportFilterRequest
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
        public string? SortOrder { get; set; }
    }

    public class SaveReportRequest
    {
        public string Name { get; set; } = string.Empty;
        public ReportType ReportType { get; set; }
        public ReportAccessLevel AccessLevel { get; set; }
        public string? Description { get; set; }
        public string? Parameters { get; set; }
        public bool IsScheduled { get; set; } = false;
        public string? ScheduleCron { get; set; }
    }

    public class ReportDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public string AccessLevel { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string GeneratedBy { get; set; } = string.Empty;
        public string? FilePath { get; set; }
    }
} 
