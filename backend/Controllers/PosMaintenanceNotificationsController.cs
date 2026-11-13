using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>POS read/acknowledge for platform maintenance notifications.</summary>
[Authorize]
[ApiController]
[Route("api/pos/maintenance-notifications")]
[Produces("application/json")]
public sealed class PosMaintenanceNotificationsController : ControllerBase
{
    private readonly IMaintenanceNotificationService _service;

    public PosMaintenanceNotificationsController(IMaintenanceNotificationService service)
    {
        _service = service;
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(IReadOnlyList<MaintenanceNotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MaintenanceNotificationDto>>> GetActive(
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var items = await _service
            .GetActiveForUserAsync(userId, MaintenanceAffectedSystems.Pos, cancellationToken)
            .ConfigureAwait(false);
        return Ok(items);
    }

    [HttpPost("{id:guid}/acknowledge")]
    [ProducesResponseType(typeof(MaintenanceNotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceNotificationDto>> Acknowledge(
        [FromRoute] Guid id,
        [FromBody] AcknowledgeMaintenanceNotificationRequestDto? body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        body ??= new AcknowledgeMaintenanceNotificationRequestDto();
        try
        {
            var result = await _service
                .AcknowledgeAsync(id, userId, body, cancellationToken)
                .ConfigureAwait(false);
            if (result is null)
                return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "ACKNOWLEDGE_FAILED", message = ex.Message });
        }
    }
}
