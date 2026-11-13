using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE signing-capacity planning and alerts.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/capacity")]
[Produces("application/json")]
public sealed class AdminTseCapacityController : ControllerBase
{
    private readonly ITseCapacityPlanningService _capacity;

    public AdminTseCapacityController(ITseCapacityPlanningService capacity)
    {
        _capacity = capacity;
    }

    [HttpGet("report")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseCapacityReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseCapacityReportDto>> GetReport(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            var report = await _capacity.GetCapacityReportAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(report);
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

    [HttpGet("forecast")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseForecastResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseForecastResultDto>> GetForecast(
        [FromQuery] Guid tenantId,
        [FromQuery] int forecastDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            var forecast = await _capacity.ForecastCapacityAsync(tenantId, forecastDays, cancellationToken)
                .ConfigureAwait(false);
            return Ok(forecast);
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

    [HttpPost("check-alerts")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseCapacityAlertDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseCapacityAlertDto>> CheckAlerts(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            var alert = await _capacity.CheckCapacityAlertsAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(alert);
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
