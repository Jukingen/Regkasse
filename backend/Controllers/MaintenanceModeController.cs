using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Platform maintenance mode status (shared) and SuperAdmin start/end controls.
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
public sealed class MaintenanceModeController : ControllerBase
{
    private readonly IMaintenanceModeService _mode;

    public MaintenanceModeController(IMaintenanceModeService mode)
    {
        _mode = mode;
    }

    /// <summary>Current platform maintenance mode (any authenticated user).</summary>
    [HttpGet("api/maintenance/status")]
    [ProducesResponseType(typeof(MaintenanceModeStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MaintenanceModeStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _mode.GetCurrentStatusAsync(cancellationToken).ConfigureAwait(false);
        return Ok(status);
    }

    /// <summary>Alias under admin prefix for FA clients.</summary>
    [HttpGet("api/admin/maintenance/status")]
    [ProducesResponseType(typeof(MaintenanceModeStatusDto), StatusCodes.Status200OK)]
    public Task<ActionResult<MaintenanceModeStatusDto>> GetAdminStatus(CancellationToken cancellationToken) =>
        GetStatus(cancellationToken);

    /// <summary>Alias under POS prefix for POS clients.</summary>
    [HttpGet("api/pos/maintenance/status")]
    [ProducesResponseType(typeof(MaintenanceModeStatusDto), StatusCodes.Status200OK)]
    public Task<ActionResult<MaintenanceModeStatusDto>> GetPosStatus(CancellationToken cancellationToken) =>
        GetStatus(cancellationToken);

    [HttpPost("api/admin/maintenance/start")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(MaintenanceModeStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MaintenanceModeStatusDto>> Start(
        [FromBody] StartMaintenanceModeRequestDto? body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        body ??= new StartMaintenanceModeRequestDto();
        try
        {
            var status = await _mode.StartAsync(userId, body, cancellationToken).ConfigureAwait(false);
            return Ok(status);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_MAINTENANCE", message = ex.Message });
        }
    }

    [HttpPost("api/admin/maintenance/end")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(MaintenanceModeStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MaintenanceModeStatusDto>> End(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var status = await _mode.EndAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(status);
    }
}
