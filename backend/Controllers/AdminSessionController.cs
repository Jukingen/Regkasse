using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin platform session management (list, terminate, force logout).</summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/sessions")]
[Produces("application/json")]
[HasPermission(AppPermissions.SystemCritical)]
public sealed class AdminSessionController : ControllerBase
{
    private readonly ISessionManagementService _sessionService;
    private readonly ILogger<AdminSessionController> _logger;

    public AdminSessionController(
        ISessionManagementService sessionService,
        ILogger<AdminSessionController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>List all active auth sessions across users.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminActiveSessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminActiveSessionDto>>> GetActiveSessions(
        CancellationToken cancellationToken)
    {
        var sessions = await _sessionService
            .GetActiveSessionsAsync(TryGetCurrentSessionId(), cancellationToken)
            .ConfigureAwait(false);
        return Ok(sessions);
    }

    /// <summary>List active sessions for one user (Identity user id).</summary>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminActiveSessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminActiveSessionDto>>> GetUserSessions(
        string userId,
        CancellationToken cancellationToken)
    {
        var sessions = await _sessionService.GetUserSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(sessions);
    }

    /// <summary>Revoke one session and its refresh tokens.</summary>
    [HttpPost("{sessionId:guid}/terminate")]
    [ProducesResponseType(typeof(TerminateSessionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TerminateSessionResultDto>> TerminateSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        if (actor == null)
            return Unauthorized();

        var result = await _sessionService
            .TerminateSessionAsync(sessionId, actor.Value.UserId, actor.Value.Role, cancellationToken)
            .ConfigureAwait(false);
        if (!result)
            return NotFound(new { code = "SESSION_NOT_FOUND", message = "Session not found or already terminated." });

        return Ok(new TerminateSessionResultDto { Success = true });
    }

    /// <summary>Revoke every active session for one user (does not rotate security stamp).</summary>
    [HttpPost("user/{userId}/terminate-all")]
    [ProducesResponseType(typeof(TerminateSessionsCountDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TerminateSessionsCountDto>> TerminateAllUserSessions(
        string userId,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        if (actor == null)
            return Unauthorized();

        var count = await _sessionService
            .TerminateAllUserSessionsAsync(userId, actor.Value.UserId, actor.Value.Role, cancellationToken)
            .ConfigureAwait(false);
        return Ok(new TerminateSessionsCountDto { TerminatedCount = count });
    }

    /// <summary>
    /// Rotate security stamp (invalidates JWTs with <c>sst</c> claim) and revoke all sessions + refresh tokens.
    /// </summary>
    [HttpPost("user/{userId}/force-logout")]
    [ProducesResponseType(typeof(ForceLogoutResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ForceLogoutResultDto>> ForceLogout(
        string userId,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        if (actor == null)
            return Unauthorized();

        var ok = await _sessionService
            .ForceLogoutAsync(userId, actor.Value.UserId, actor.Value.Role, cancellationToken)
            .ConfigureAwait(false);
        if (!ok)
            return NotFound(new { code = "USER_NOT_FOUND", message = "User not found." });

        return Ok(new ForceLogoutResultDto { Success = true });
    }

    /// <summary>Revoke all active sessions except the caller's current session.</summary>
    [HttpPost("terminate-all")]
    [ProducesResponseType(typeof(TerminateSessionsCountDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TerminateSessionsCountDto>> TerminateAllSessions(
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        if (actor == null)
            return Unauthorized();

        var exceptSessionId = TryGetCurrentSessionId();
        var count = await _sessionService
            .TerminateAllSessionsAsync(actor.Value.UserId, actor.Value.Role, exceptSessionId, cancellationToken)
            .ConfigureAwait(false);
        _logger.LogWarning(
            "Super Admin {UserId} terminated {Count} platform sessions except current {SessionId}",
            actor.Value.UserId,
            count,
            exceptSessionId);
        return Ok(new TerminateSessionsCountDto { TerminatedCount = count });
    }

    private (string UserId, string Role)? RequireActor()
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return null;
        var role = User.GetActorRole() ?? Roles.SuperAdmin;
        return (userId, role);
    }

    private Guid? TryGetCurrentSessionId()
    {
        var sid = User.FindFirst("sid")?.Value;
        return Guid.TryParse(sid, out var id) ? id : null;
    }
}
