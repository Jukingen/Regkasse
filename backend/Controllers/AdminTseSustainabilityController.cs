using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE sustainability / green-IT reporting (indicative — not certified LCA).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/sustainability")]
[Produces("application/json")]
public sealed class AdminTseSustainabilityController : ControllerBase
{
    private readonly ITseSustainabilityService _sustainability;

    public AdminTseSustainabilityController(ITseSustainabilityService sustainability)
    {
        _sustainability = sustainability;
    }

    [HttpGet("report")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseSustainabilityReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseSustainabilityReportDto>> GetReport(
        [FromQuery] Guid tenantId,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _sustainability
                .GetSustainabilityReportAsync(tenantId, fromUtc, toUtc, cancellationToken)
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

    [HttpGet("carbon")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseCarbonFootprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseCarbonFootprintDto>> CalculateCarbon(
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
            return Ok(await _sustainability
                .CalculateCarbonFootprintAsync(tenantId, from, to, cancellationToken)
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

    [HttpGet("optimizations")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseSustainabilityOptimizationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseSustainabilityOptimizationResultDto>> GetOptimizations(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _sustainability
                .GetOptimizationSuggestionsAsync(tenantId, cancellationToken)
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
