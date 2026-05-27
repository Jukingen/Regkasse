using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/user/sessions")]
[Produces("application/json")]
public class UserSessionsController : ControllerBase
{
    private readonly ISessionService _sessions;
    private readonly ITenantSessionPolicyService _policy;
    private readonly ILogger<UserSessionsController> _logger;

    public UserSessionsController(
        ISessionService sessions,
        ITenantSessionPolicyService policy,
        ILogger<UserSessionsController> logger)
    {
        _sessions = sessions;
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

    [HttpGet("/api/user/session-policy")]
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

        var count = await _sessions.TerminateAllOtherSessionsAsync(userId, currentSessionId.Value, cancellationToken)
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

        var ok = await _sessions.TerminateSessionAsync(userId, id, cancellationToken).ConfigureAwait(false);
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
