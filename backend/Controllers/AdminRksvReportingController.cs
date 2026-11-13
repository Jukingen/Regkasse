using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin RKSV historical reporting (sale-time tax rates; product price history).
/// </summary>
[ApiController]
[Route("api/admin/reports/rksv")]
public class AdminRksvReportingController : ControllerBase
{
    private readonly IRksvReportingService _rksvReportingService;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly ILogger<AdminRksvReportingController> _logger;

    public AdminRksvReportingController(
        IRksvReportingService rksvReportingService,
        ISettingsTenantResolver settingsTenantResolver,
        ILogger<AdminRksvReportingController> logger)
    {
        _rksvReportingService = rksvReportingService;
        _settingsTenantResolver = settingsTenantResolver;
        _logger = logger;
    }

    [HttpGet("historical")]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(RksvReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<RksvReport>> GetHistoricalReport(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            var report = await _rksvReportingService
                .GenerateHistoricalReportAsync(tenantId, fromUtc, toUtc, cancellationToken)
                .ConfigureAwait(false);
            return Ok(report);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building RKSV historical report");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("tax-breakdown")]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(TaxBreakdown), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaxBreakdown>> GetTaxBreakdown(
        [FromQuery] DateTime dateUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            var breakdown = await _rksvReportingService
                .GetTaxBreakdownForPeriodAsync(tenantId, dateUtc, cancellationToken)
                .ConfigureAwait(false);
            return Ok(breakdown);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building RKSV tax breakdown");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("price-history/{productId:guid}")]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(PriceHistoryReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<PriceHistoryReport>> GetPriceHistory(
        Guid productId,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            var report = await _rksvReportingService
                .GetPriceHistoryForProductAsync(tenantId, productId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(report);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Product not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading RKSV price history for product {ProductId}", productId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
