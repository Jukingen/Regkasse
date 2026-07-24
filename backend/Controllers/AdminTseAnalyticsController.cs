using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE business-intelligence dashboard / report / export (diagnostic only).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/analytics")]
[Produces("application/json")]
public sealed class AdminTseAnalyticsController : ControllerBase
{
    private readonly ITseReportingService _reporting;

    public AdminTseAnalyticsController(ITseReportingService reporting)
    {
        _reporting = reporting;
    }

    [HttpGet("dashboard")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseBiDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseBiDashboardDto>> GetDashboard(
        [FromQuery] Guid tenantId,
        [FromQuery] int lookbackDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _reporting
                .GetDashboardDataAsync(tenantId, lookbackDays, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("report")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseBiReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseBiReportDto>> GenerateReport(
        [FromBody] TseBiReportRequestDto? body,
        CancellationToken cancellationToken)
    {
        body ??= new TseBiReportRequestDto();
        if (body.TenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _reporting.GenerateReportAsync(body, cancellationToken).ConfigureAwait(false));
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

    [HttpPost("export")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseBiExportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseBiExportResultDto>> Export(
        [FromBody] TseBiExportRequestDto? body,
        CancellationToken cancellationToken)
    {
        body ??= new TseBiExportRequestDto();
        if (body.TenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _reporting.ExportReportAsync(body, cancellationToken).ConfigureAwait(false));
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
