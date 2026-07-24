using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE SLA monitoring and reporting.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/sla")]
[Produces("application/json")]
public sealed class AdminTseSlaController : ControllerBase
{
    private readonly ITseSlaMonitorService _sla;

    public AdminTseSlaController(ITseSlaMonitorService sla)
    {
        _sla = sla;
    }

    [HttpGet("report")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseSlaReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseSlaReportDto>> GetReport(
        [FromQuery] Guid tenantId,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        var to = toUtc ?? DateTime.UtcNow;
        var from = fromUtc ?? to.AddDays(-7);
        try
        {
            var report = await _sla.GetSlaReportAsync(tenantId, from, to, cancellationToken)
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

    [HttpGet("status")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseSlaStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseSlaStatusDto>> GetStatus(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            var status = await _sla.GetCurrentSlaStatusAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(status);
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

    [HttpPost("check-violations")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseSlaAlertDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseSlaAlertDto>> CheckViolations(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            var alert = await _sla.CheckSlaViolationsAsync(tenantId, cancellationToken)
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
