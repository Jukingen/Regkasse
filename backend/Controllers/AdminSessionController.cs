using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Session management for Super Admin (all tenants) and Mandanten-Admin (own tenant).
/// Includes POS (<c>client_app=pos</c>) and Admin sessions. Cashier cannot access (<c>user.view</c> / <c>user.manage</c>).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/sessions")]
[Produces("application/json")]
public sealed class AdminSessionController : ControllerBase
{
    private readonly ISessionManagementService _sessionService;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ILogger<AdminSessionController> _logger;

    public AdminSessionController(
        ISessionManagementService sessionService,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<AdminSessionController> logger)
    {
        _sessionService = sessionService;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    /// <summary>List auth sessions (POS + Admin) with optional filters. Manager is limited to the ambient tenant.</summary>
    [HttpGet]
    [HasPermission(AppPermissions.UserView)]
    [ProducesResponseType(typeof(IReadOnlyList<AdminActiveSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AdminActiveSessionDto>>> GetActiveSessions(
        [FromQuery] string? search,
        [FromQuery] Guid? tenantId,
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] string? userId,
        CancellationToken cancellationToken)
    {
        var access = TryBuildAccess();
        if (access == null)
            return Unauthorized();
        if (!access.IsSuperAdmin && access.ScopedTenantId == null)
            return NotFound();

        var filter = new AdminSessionListFilter
        {
            Search = search,
            TenantId = access.IsSuperAdmin ? tenantId : access.ScopedTenantId,
            Role = role,
            Status = status,
            UserId = userId,
        };

        var sessions = await _sessionService
            .ListSessionsAsync(access, filter, cancellationToken)
            .ConfigureAwait(false);
        return Ok(sessions);
    }

    /// <summary>List active sessions for one user (Identity user id). Manager: own tenant only.</summary>
    [HttpGet("user/{userId}")]
    [HasPermission(AppPermissions.UserView)]
    [ProducesResponseType(typeof(IReadOnlyList<AdminActiveSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AdminActiveSessionDto>>> GetUserSessions(
        string userId,
        CancellationToken cancellationToken)
    {
        var access = TryBuildAccess();
        if (access == null)
            return Unauthorized();
        if (!access.IsSuperAdmin && access.ScopedTenantId == null)
            return NotFound();

        var sessions = await _sessionService
            .GetUserSessionsAsync(userId, access, cancellationToken)
            .ConfigureAwait(false);
        return Ok(sessions);
    }

    /// <summary>Revoke one session and its refresh tokens. Cannot terminate the caller's current session.</summary>
    [HttpPost("{sessionId:guid}/terminate")]
    [HasPermission(AppPermissions.UserManage)]
    [ProducesResponseType(typeof(TerminateSessionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<TerminateSessionResultDto>> TerminateSession(
        Guid sessionId,
        CancellationToken cancellationToken) =>
        LogoutSessionCoreAsync(sessionId, cancellationToken);

    /// <summary>Alias of <see cref="TerminateSession"/> (<c>POST …/logout</c>).</summary>
    [HttpPost("{sessionId:guid}/logout")]
    [HasPermission(AppPermissions.UserManage)]
    [ProducesResponseType(typeof(TerminateSessionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<TerminateSessionResultDto>> LogoutSession(
        Guid sessionId,
        CancellationToken cancellationToken) =>
        LogoutSessionCoreAsync(sessionId, cancellationToken);

    /// <summary>Revoke every active session for one user (does not rotate security stamp). Super Admin only.</summary>
    [HttpPost("user/{userId}/terminate-all")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [HasPermission(AppPermissions.SystemCritical)]
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
    /// Super Admin only.
    /// </summary>
    [HttpPost("user/{userId}/force-logout")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [HasPermission(AppPermissions.SystemCritical)]
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

    /// <summary>Revoke all in-scope active sessions except the caller's current session.</summary>
    [HttpPost("terminate-all")]
    [HasPermission(AppPermissions.UserManage)]
    [ProducesResponseType(typeof(TerminateSessionsCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<TerminateSessionsCountDto>> TerminateAllSessions(
        CancellationToken cancellationToken) =>
        LogoutAllCoreAsync(cancellationToken);

    /// <summary>Alias of <see cref="TerminateAllSessions"/> (<c>POST …/logout-all</c>).</summary>
    [HttpPost("logout-all")]
    [HasPermission(AppPermissions.UserManage)]
    [ProducesResponseType(typeof(TerminateSessionsCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<TerminateSessionsCountDto>> LogoutAllSessions(
        CancellationToken cancellationToken) =>
        LogoutAllCoreAsync(cancellationToken);

    /// <summary>Revoke selected sessions (skips current session and out-of-scope ids).</summary>
    [HttpPost("logout-bulk")]
    [HasPermission(AppPermissions.UserManage)]
    [ProducesResponseType(typeof(TerminateSessionsCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TerminateSessionsCountDto>> LogoutBulkSessions(
        [FromBody] LogoutBulkSessionsRequestDto request,
        CancellationToken cancellationToken)
    {
        var access = TryBuildAccess();
        if (access == null)
            return Unauthorized();
        if (!access.IsSuperAdmin && access.ScopedTenantId == null)
            return NotFound();
        if (request.SessionIds == null || request.SessionIds.Count == 0)
            return BadRequest(new { code = "SESSION_IDS_REQUIRED", message = "At least one session id is required." });

        var count = await _sessionService
            .TerminateBulkAsync(
                request.SessionIds,
                access.ActorUserId,
                access.ActorRole,
                access.CurrentSessionId,
                access.ScopedTenantId,
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(new TerminateSessionsCountDto { TerminatedCount = count });
    }

    private async Task<ActionResult<TerminateSessionResultDto>> LogoutSessionCoreAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var access = TryBuildAccess();
        if (access == null)
            return Unauthorized();
        if (!access.IsSuperAdmin && access.ScopedTenantId == null)
            return NotFound();

        var result = await _sessionService
            .TerminateSessionAsync(
                sessionId,
                access.ActorUserId,
                access.ActorRole,
                cancellationToken,
                access.CurrentSessionId,
                access.ScopedTenantId)
            .ConfigureAwait(false);

        if (result.IsCurrentSession)
        {
            return BadRequest(new
            {
                code = "CANNOT_TERMINATE_CURRENT_SESSION",
                message = "The current session cannot be terminated from this page.",
            });
        }

        if (!result.Success)
            return NotFound(new { code = "SESSION_NOT_FOUND", message = "Session not found or already terminated." });

        return Ok(new TerminateSessionResultDto { Success = true });
    }

    private async Task<ActionResult<TerminateSessionsCountDto>> LogoutAllCoreAsync(
        CancellationToken cancellationToken)
    {
        var access = TryBuildAccess();
        if (access == null)
            return Unauthorized();
        if (!access.IsSuperAdmin && access.ScopedTenantId == null)
            return NotFound();

        var count = await _sessionService
            .TerminateAllSessionsAsync(
                access.ActorUserId,
                access.ActorRole,
                access.CurrentSessionId,
                cancellationToken,
                access.ScopedTenantId)
            .ConfigureAwait(false);
        _logger.LogWarning(
            "Admin {UserId} ({Role}) terminated {Count} sessions except current {SessionId} tenant={TenantId}",
            access.ActorUserId,
            access.ActorRole,
            count,
            access.CurrentSessionId,
            access.ScopedTenantId);
        return Ok(new TerminateSessionsCountDto { TerminatedCount = count });
    }

    private SessionManagementAccess? TryBuildAccess()
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return null;

        var role = User.GetActorRole() ?? string.Empty;
        var isSuperAdmin = string.Equals(role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase);
        return new SessionManagementAccess
        {
            ActorUserId = userId,
            ActorRole = string.IsNullOrEmpty(role) ? (isSuperAdmin ? Roles.SuperAdmin : role) : role,
            IsSuperAdmin = isSuperAdmin,
            CurrentSessionId = TryGetCurrentSessionId(),
            ScopedTenantId = isSuperAdmin ? null : _tenantAccessor.TenantId,
        };
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
