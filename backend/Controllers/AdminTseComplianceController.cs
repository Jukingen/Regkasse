using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE compliance report (signature coverage + chain continuity + device health).
/// Diagnostic only — not a legally binding RKSV / Finanzamt proof.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/compliance")]
[Produces("application/json")]
public sealed class AdminTseComplianceController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITseComplianceReportService _compliance;

    public AdminTseComplianceController(
        AppDbContext db,
        ITseComplianceReportService compliance)
    {
        _db = db;
        _compliance = compliance;
    }

    /// <summary>
    /// Full compliance report for a tenant period.
    /// <paramref name="fromUtc"/> inclusive, <paramref name="toUtc"/> exclusive.
    /// </summary>
    [HttpGet("report")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseComplianceReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseComplianceReportDto>> GetReport(
        [FromQuery] Guid tenantId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });
        if (toUtc <= fromUtc)
            return BadRequest(new { error = "toUtc must be strictly greater than fromUtc." });

        var exists = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            return NotFound();

        try
        {
            var report = await _compliance
                .GenerateComplianceReportAsync(tenantId, fromUtc, toUtc, cancellationToken)
                .ConfigureAwait(false);
            return Ok(report);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Rolling 7-day compliance status for a tenant.</summary>
    [HttpGet("status")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseComplianceStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseComplianceStatusDto>> GetStatus(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        var exists = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            return NotFound();

        try
        {
            var status = await _compliance
                .GetComplianceStatusAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(status);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
