using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Session;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/user/sessions")]
[Produces("application/json")]
public class UserSessionsController : ControllerBase
{
    private readonly Services.ISessionService _sessions;
    private readonly IDeviceSessionService _deviceSessions;
    private readonly ITenantSessionPolicyService _policy;
    private readonly ILogger<UserSessionsController> _logger;

    public UserSessionsController(
        Services.ISessionService sessions,
        IDeviceSessionService deviceSessions,
        ITenantSessionPolicyService policy,
        ILogger<UserSessionsController> logger)
    {
        _sessions = sessions;
        _deviceSessions = deviceSessions;
        _policy = policy;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ActiveSession>>> List(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var currentSessionId = TryGetCurrentSessionId();
        var list = await _sessions.GetMyActiveSessionsAsync(userId, currentSessionId, cancellationToken).ConfigureAwait(false);
        return Ok(list);
    }

    /// <summary>Sketch-aligned DTO shape (device name / browser / OS / last active).</summary>
    [HttpGet("devices")]
    public async Task<ActionResult<IReadOnlyList<UserSessionDto>>> ListDevices(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var list = await _deviceSessions
            .GetActiveSessionsAsync(userId, TryGetCurrentSessionId(), cancellationToken)
            .ConfigureAwait(false);
        return Ok(list);
    }

    /// <summary>
    /// Returns the effective session policy for the current user:
    /// concurrent-session limits from <c>SessionPolicy</c> configuration, plus tenant idle-timeout settings when available.
    /// </summary>
    /// <response code="200">Session policy (maxConcurrentSessions, sessionTimeoutMinutes, allowMultipleDevices, …).</response>
    /// <response code="401">Caller is not authenticated.</response>
    [HttpGet("/api/user/session-policy")]
    [ProducesResponseType(typeof(TenantSessionPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantSessionPolicyDto>> GetSessionPolicy(CancellationToken cancellationToken)
    {
        var policy = await _policy.GetPolicyAsync(null, cancellationToken).ConfigureAwait(false);
        return Ok(policy);
    }

    [HttpPost("terminate-all")]
    public async Task<ActionResult> TerminateAllOthers(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var currentSessionId = TryGetCurrentSessionId();
        if (!currentSessionId.HasValue)
            return BadRequest(new { message = "Current session id not found in token.", code = "SESSION_ID_MISSING" });

        var count = await _deviceSessions
            .RevokeOtherSessionsAsync(userId, currentSessionId.Value, cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation("User {UserId} terminated {Count} other sessions", userId, count);
        return Ok(new { terminatedCount = count });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Terminate(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var currentSessionId = TryGetCurrentSessionId();
        var accessToken = DeviceSessionService.TryGetBearerToken(Request.Headers.Authorization.ToString());
        var expiry = DeviceSessionService.TryGetAccessTokenExpiry(User);

        var ok = await _deviceSessions
            .RevokeSessionAsync(
                userId,
                id,
                currentSessionId,
                accessToken,
                expiry,
                cancellationToken)
            .ConfigureAwait(false);
        if (!ok)
            return NotFound();
        return NoContent();
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentSessionId().HasValue)
            return NoContent();

        await _sessions.TouchSessionActivityAsync(TryGetCurrentSessionId()!.Value, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private Guid? TryGetCurrentSessionId()
    {
        var sid = User.FindFirst("sid")?.Value;
        return Guid.TryParse(sid, out var id) ? id : null;
    }
}
