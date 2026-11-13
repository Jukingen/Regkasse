using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin MwSt / tax analytics and CSV export.</summary>
[ApiController]
[Route("api/admin/reports/tax")]
public class AdminTaxReportsController : ControllerBase
{
    private readonly ITaxReportService _taxReportService;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly ILogger<AdminTaxReportsController> _logger;

    public AdminTaxReportsController(
        ITaxReportService taxReportService,
        ISettingsTenantResolver settingsTenantResolver,
        ILogger<AdminTaxReportsController> logger)
    {
        _taxReportService = taxReportService;
        _settingsTenantResolver = settingsTenantResolver;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(TaxReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaxReport>> GetReport(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            var report = await _taxReportService
                .GetReportAsync(tenantId, fromUtc, toUtc, cancellationToken)
                .ConfigureAwait(false);
            return Ok(report);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building tax report");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("trend")]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(IReadOnlyList<TaxTrendPoint>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaxTrendPoint>>> GetTrend(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] string granularity = "day",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            var trend = await _taxReportService
                .GetTrendAsync(tenantId, fromUtc, toUtc, granularity, cancellationToken)
                .ConfigureAwait(false);
            return Ok(trend);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building tax trend");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("export")]
    [HasPermission(AppPermissions.ReportExport)]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] string period = "custom",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (start, end) = ResolveExportPeriod(period, fromUtc, toUtc);
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            var bytes = await _taxReportService
                .ExportCsvAsync(tenantId, start, end, cancellationToken)
                .ConfigureAwait(false);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd");
            var fileName = period.Equals("year", StringComparison.OrdinalIgnoreCase)
                ? $"jahressteuerbericht_{stamp}.csv"
                : period.Equals("month", StringComparison.OrdinalIgnoreCase)
                    ? $"monatssteuerbericht_{stamp}.csv"
                    : $"steuerbericht_{stamp}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting tax report");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private static (DateTime From, DateTime To) ResolveExportPeriod(
        string period,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var now = DateTime.UtcNow;
        if (period.Equals("year", StringComparison.OrdinalIgnoreCase))
        {
            var start = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddYears(1);
            return (start, end);
        }

        if (period.Equals("month", StringComparison.OrdinalIgnoreCase))
        {
            var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);
            return (start, end);
        }

        if (fromUtc == default || toUtc == default)
            throw new ArgumentException("fromUtc and toUtc are required for custom export periods.");

        return (fromUtc, toUtc);
    }
}
