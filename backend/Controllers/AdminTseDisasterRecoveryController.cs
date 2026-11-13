using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE disaster-recovery runbooks and simulation drills (no live failover by default).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/disaster-recovery")]
[Produces("application/json")]
public sealed class AdminTseDisasterRecoveryController : ControllerBase
{
    private readonly ITseDisasterRecoveryService _dr;

    public AdminTseDisasterRecoveryController(ITseDisasterRecoveryService dr)
    {
        _dr = dr;
    }

    [HttpGet("status")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseDrStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseDrStatusDto>> GetStatus(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _dr.GetDrStatusAsync(tenantId, cancellationToken).ConfigureAwait(false));
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

    [HttpPost("runbooks")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseDrRunbookDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseDrRunbookDto>> GenerateRunbook(
        [FromQuery] Guid tenantId,
        [FromBody] GenerateTseDrRunbookRequestDto? body = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            var actor = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var runbook = await _dr
                .GenerateRunbookAsync(tenantId, body?.Scenario ?? "TSEFailure", actor, cancellationToken)
                .ConfigureAwait(false);
            return CreatedAtAction(nameof(GetStatus), new { tenantId }, runbook);
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

    [HttpPost("runbooks/{runbookId:guid}/execute")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseDrExecutionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseDrExecutionResultDto>> ExecuteRunbook(
        Guid runbookId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var actor = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Ok(await _dr.ExecuteRunbookAsync(runbookId, actor, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("drill")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseDrReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseDrReportDto>> RunDrill(
        [FromQuery] Guid tenantId,
        [FromQuery] string scenario = "TSEFailure",
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            var actor = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Ok(await _dr.RunDrDrillAsync(tenantId, scenario, actor, cancellationToken).ConfigureAwait(false));
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
