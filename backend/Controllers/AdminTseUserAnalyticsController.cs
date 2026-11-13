using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE / POS user-behavior analytics (diagnostic only).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/user-analytics")]
[Produces("application/json")]
public sealed class AdminTseUserAnalyticsController : ControllerBase
{
    private readonly ITseUserAnalyticsService _analytics;

    public AdminTseUserAnalyticsController(ITseUserAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [HttpGet("report")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseUserBehaviorReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseUserBehaviorReportDto>> GenerateUserReport(
        [FromQuery] Guid tenantId,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        var to = toUtc ?? DateTime.UtcNow;
        var from = fromUtc ?? to.AddDays(-30);

        try
        {
            return Ok(await _analytics
                .GenerateUserReportAsync(tenantId, from, to, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("features")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseFeatureUsageReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseFeatureUsageReportDto>> GetFeatureUsage(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _analytics
                .GetFeatureUsageReportAsync(tenantId, fromUtc, toUtc, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("cohorts")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseCohortAnalysisResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseCohortAnalysisResultDto>> PerformCohortAnalysis(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _analytics
                .PerformCohortAnalysisAsync(tenantId, fromUtc, toUtc, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
