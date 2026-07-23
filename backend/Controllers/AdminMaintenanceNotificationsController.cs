using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin CRUD for platform maintenance notifications; authenticated FA users read active notices.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/maintenance-notifications")]
[Produces("application/json")]
public sealed class AdminMaintenanceNotificationsController : ControllerBase
{
    private readonly IMaintenanceNotificationService _service;

    public AdminMaintenanceNotificationsController(IMaintenanceNotificationService service)
    {
        _service = service;
    }

    /// <summary>Active notices for the current FA user (banner / force modal).</summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IReadOnlyList<MaintenanceNotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MaintenanceNotificationDto>>> GetActive(
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var items = await _service
            .GetActiveForUserAsync(userId, MaintenanceAffectedSystems.Fa, cancellationToken)
            .ConfigureAwait(false);
        return Ok(items);
    }

    /// <summary>Dismiss / mark-read for the current user.</summary>
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

    /// <summary>Super Admin: list all maintenance notifications.</summary>
    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(MaintenanceNotificationListResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MaintenanceNotificationListResponseDto>> List(
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service
                .ListAsync(status, limit, offset, cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_FILTER", message = ex.Message });
        }
    }

    /// <summary>Super Admin: create a draft (or publish immediately).</summary>
    [HttpPost]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(MaintenanceNotificationDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<MaintenanceNotificationDto>> Create(
        [FromBody] CreateMaintenanceNotificationRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var created = await _service.CreateAsync(userId, body, cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(List), new { }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_NOTIFICATION", message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(MaintenanceNotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceNotificationDto>> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateMaintenanceNotificationRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        try
        {
            var updated = await _service.UpdateAsync(id, body, cancellationToken).ConfigureAwait(false);
            if (updated is null)
                return NotFound();
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_NOTIFICATION", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "UPDATE_FAILED", message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/publish")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(MaintenanceNotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceNotificationDto>> Publish(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.PublishAsync(id, cancellationToken).ConfigureAwait(false);
            if (result is null)
                return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "PUBLISH_FAILED", message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(MaintenanceNotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceNotificationDto>> Cancel(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CancelAsync(id, cancellationToken).ConfigureAwait(false);
            if (result is null)
                return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "CANCEL_FAILED", message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/in-progress")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(MaintenanceNotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceNotificationDto>> MarkInProgress(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.MarkInProgressAsync(id, cancellationToken).ConfigureAwait(false);
            if (result is null)
                return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "STATUS_FAILED", message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/complete")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(MaintenanceNotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceNotificationDto>> Complete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CompleteAsync(id, cancellationToken).ConfigureAwait(false);
            if (result is null)
                return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "COMPLETE_FAILED", message = ex.Message });
        }
    }
}
