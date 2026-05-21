using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.AdminCashRegisters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Tenant-scoped cash register admin operations (Schlussbeleg decommission, dev-only hard delete).</summary>
[ApiController]
[Route("api/admin/cash-registers")]
[Authorize]
[Produces("application/json")]
public sealed class AdminCashRegistersController : ControllerBase
{
    private readonly ICashRegisterDecommissionService _decommission;
    private readonly ILogger<AdminCashRegistersController> _logger;

    public AdminCashRegistersController(
        ICashRegisterDecommissionService decommission,
        ILogger<AdminCashRegistersController> logger)
    {
        _decommission = decommission;
        _logger = logger;
    }

    /// <summary>Feature flags for FA (hard delete visible only in Development + AllowHardDelete).</summary>
    [HttpGet("capabilities")]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(AdminCashRegisterCapabilitiesDto), StatusCodes.Status200OK)]
    public ActionResult<AdminCashRegisterCapabilitiesDto> GetCapabilities()
    {
        return Ok(new AdminCashRegisterCapabilitiesDto
        {
            AllowHardDelete = _decommission.IsHardDeleteAllowed(),
            DecommissionViaSchlussbeleg = true,
        });
    }

    /// <summary>
    /// Permanently decommissions a cash register via RKSV Schlussbeleg (Endbeleg) and sets status to Decommissioned atomically.
    /// </summary>
    [HttpPut("{id:guid}/decommission")]
    [HasPermission(AppPermissions.RksvSchlussbelegCreate)]
    [ProducesResponseType(typeof(DecommissionCashRegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DecommissionCashRegisterResponse>> Decommission(
        Guid id,
        [FromBody] DecommissionCashRegisterRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetActorUserId();
        if (string.IsNullOrEmpty(actorUserId))
            return Unauthorized();

        var actorRole = User.GetActorRole() ?? "Unknown";

        try
        {
            var result = await _decommission.DecommissionAsync(
                id,
                request?.Reason,
                actorUserId,
                actorRole,
                cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (RksvOperationGuardException ex)
        {
            _logger.LogWarning(ex, "Decommission rejected for register {RegisterId}", id);
            var status = ex.ErrorCode switch
            {
                RksvGuardErrorCodes.RegisterAlreadyDecommissioned => StatusCodes.Status409Conflict,
                RksvGuardErrorCodes.DuplicateSchlussbeleg => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return StatusCode(status, new { message = ex.Message, code = ex.ErrorCode });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Hard-deletes an empty cash register row (Development + CashRegister:AllowHardDelete only).</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> HardDelete(
        Guid id,
        [FromBody] HardDeleteCashRegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (!_decommission.IsHardDeleteAllowed())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Hard delete is not enabled for this environment." });

        var actorUserId = User.GetActorUserId();
        if (string.IsNullOrEmpty(actorUserId))
            return Unauthorized();

        var actorRole = User.GetActorRole() ?? "Unknown";

        try
        {
            await _decommission.HardDeleteAsync(id, request.ConfirmPhrase, actorUserId, actorRole, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
