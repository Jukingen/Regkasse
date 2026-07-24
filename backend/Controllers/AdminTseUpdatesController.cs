using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE zero-downtime catalog/policy updates (diagnostic — not fiscal firmware).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/updates")]
[Produces("application/json")]
public sealed class AdminTseUpdatesController : ControllerBase
{
    private readonly ITseUpdateService _updates;

    public AdminTseUpdatesController(ITseUpdateService updates)
    {
        _updates = updates;
    }

    [HttpGet("status")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseUpdateStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseUpdateStatusDto>> CheckForUpdates(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _updates.CheckForUpdatesAsync(tenantId, cancellationToken)
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

    [HttpPost("apply")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseUpdateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseUpdateResultDto>> ApplyUpdate(
        [FromQuery] Guid tenantId,
        [FromBody] TseApplyUpdateRequestDto? body,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        body ??= new TseApplyUpdateRequestDto();
        try
        {
            return Ok(await _updates
                .ApplyUpdateAsync(tenantId, body.UpdateType, User.GetActorUserId(), cancellationToken)
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

    [HttpGet("history")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseUpdateHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseUpdateHistoryDto>> GetHistory(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _updates.GetUpdateHistoryAsync(tenantId, cancellationToken)
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
