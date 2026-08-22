using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class SessionManagementService : ISessionManagementService
{
    public const string RevokeReasonTerminateSession = "admin_terminated_session";
    public const string RevokeReasonTerminateUser = "admin_terminate_all_user";
    public const string RevokeReasonTerminateAll = "admin_terminate_all";
    public const string RevokeReasonForceLogout = "admin_force_logout";

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<SessionManagementService> _logger;

    public SessionManagementService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IRefreshTokenService refreshTokens,
        IAuditLogService auditLog,
        ILogger<SessionManagementService> logger)
    {
        _db = db;
        _userManager = userManager;
        _refreshTokens = refreshTokens;
        _auditLog = auditLog;
        _logger = logger;
    }

    public Task<IReadOnlyList<AdminActiveSessionDto>> GetActiveSessionsAsync(
        Guid? currentSessionId = null,
        CancellationToken cancellationToken = default) =>
        QueryActiveSessionsAsync(userId: null, currentSessionId, cancellationToken);

    public Task<IReadOnlyList<AdminActiveSessionDto>> GetUserSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Task.FromResult<IReadOnlyList<AdminActiveSessionDto>>(Array.Empty<AdminActiveSessionDto>());

        return QueryActiveSessionsAsync(userId, currentSessionId: null, cancellationToken);
    }

    public async Task<bool> TerminateSessionAsync(
        Guid sessionId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.AuthSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == sessionId && s.RevokedAtUtc == null, cancellationToken)
            .ConfigureAwait(false);
        if (session == null)
            return false;

        await _refreshTokens.LogoutSessionAsync(sessionId, RevokeReasonTerminateSession, cancellationToken)
            .ConfigureAwait(false);

        await _auditLog.LogUserLifecycleAsync(
            AuditEventType.UserSessionTerminated,
            actorUserId,
            actorRole,
            session.UserId,
            reason: RevokeReasonTerminateSession,
            description: $"Session {sessionId:D} terminated",
            newValues: new { sessionId, clientApp = session.ClientApp }).ConfigureAwait(false);

        _logger.LogInformation(
            "Super Admin {ActorUserId} terminated session {SessionId} for user {TargetUserId}",
            actorUserId,
            sessionId,
            session.UserId);
        return true;
    }

    public async Task<int> TerminateAllUserSessionsAsync(
        string userId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;

        var count = await CountActiveSessionsAsync(userId, exceptSessionId: null, cancellationToken).ConfigureAwait(false);
        if (count == 0)
            return 0;

        await _refreshTokens.LogoutAllAsync(userId, RevokeReasonTerminateUser, cancellationToken).ConfigureAwait(false);

        await _auditLog.LogUserLifecycleAsync(
            AuditEventType.UserSessionTerminated,
            actorUserId,
            actorRole,
            userId,
            reason: RevokeReasonTerminateUser,
            description: $"Terminated {count} session(s)",
            newValues: new { terminatedCount = count }).ConfigureAwait(false);

        _logger.LogInformation(
            "Super Admin {ActorUserId} terminated {Count} session(s) for user {TargetUserId}",
            actorUserId,
            count,
            userId);
        return count;
    }

    public async Task<int> TerminateAllSessionsAsync(
        string actorUserId,
        string actorRole,
        Guid? exceptSessionId,
        CancellationToken cancellationToken = default)
    {
        var sessionIds = await _db.AuthSessions
            .Where(s => s.RevokedAtUtc == null && (exceptSessionId == null || s.Id != exceptSessionId.Value))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var id in sessionIds)
            await _refreshTokens.LogoutSessionAsync(id, RevokeReasonTerminateAll, cancellationToken).ConfigureAwait(false);

        await _auditLog.LogSystemOperationAsync(
            AuditLogActions.USER_SESSION_TERMINATED,
            entityType: AuditLogEntityTypes.USER,
            userId: actorUserId,
            userRole: actorRole,
            description: $"Terminated {sessionIds.Count} session(s) (platform)",
            notes: exceptSessionId.HasValue ? $"exceptSessionId={exceptSessionId.Value:D}" : null,
            requestData: new { exceptSessionId },
            responseData: new { terminatedCount = sessionIds.Count },
            actionType: AuditEventType.UserSessionTerminated).ConfigureAwait(false);

        _logger.LogWarning(
            "Super Admin {ActorUserId} terminated {Count} platform session(s) except {ExceptSessionId}",
            actorUserId,
            sessionIds.Count,
            exceptSessionId);
        return sessionIds.Count;
    }

    public async Task<bool> ForceLogoutAsync(
        string userId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user == null)
            return false;

        var activeCount = await CountActiveSessionsAsync(userId, exceptSessionId: null, cancellationToken)
            .ConfigureAwait(false);

        var stampResult = await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        if (!stampResult.Succeeded)
        {
            _logger.LogWarning(
                "Security stamp update failed during force logout for user {UserId}: {Errors}",
                userId,
                string.Join("; ", stampResult.Errors.Select(e => e.Description)));
        }

        await _refreshTokens.LogoutAllAsync(userId, RevokeReasonForceLogout, cancellationToken).ConfigureAwait(false);

        await _auditLog.LogUserLifecycleAsync(
            AuditEventType.UserForceLogout,
            actorUserId,
            actorRole,
            userId,
            reason: RevokeReasonForceLogout,
            description: "Force logout (security stamp + all sessions)",
            newValues: new { terminatedCount = activeCount, securityStampRotated = stampResult.Succeeded })
            .ConfigureAwait(false);

        _logger.LogWarning(
            "Super Admin {ActorUserId} force-logged-out user {TargetUserId} ({SessionCount} session(s))",
            actorUserId,
            userId,
            activeCount);
        return true;
    }

    public async Task<bool> IsSessionValidAsync(
        string userId,
        Guid? sessionId,
        string? securityStamp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        var userState = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.SecurityStamp })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (userState == null || !userState.IsActive)
            return false;

        if (!string.IsNullOrEmpty(securityStamp)
            && !string.IsNullOrEmpty(userState.SecurityStamp)
            && !string.Equals(userState.SecurityStamp, securityStamp, StringComparison.Ordinal))
        {
            return false;
        }

        if (sessionId is Guid sid && sid != Guid.Empty)
            return await _refreshTokens.IsSessionActiveAsync(userId, sid, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private async Task<int> CountActiveSessionsAsync(string userId, Guid? exceptSessionId, CancellationToken cancellationToken)
    {
        return await _db.AuthSessions
            .CountAsync(
                s => s.UserId == userId
                    && s.RevokedAtUtc == null
                    && (exceptSessionId == null || s.Id != exceptSessionId.Value),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<AdminActiveSessionDto>> QueryActiveSessionsAsync(
        string? userId,
        Guid? currentSessionId,
        CancellationToken cancellationToken)
    {
        var query = _db.AuthSessions.AsNoTracking().Where(s => s.RevokedAtUtc == null);
        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(s => s.UserId == userId);

        var rows = await query
            .OrderByDescending(s => s.LastActivityAtUtc ?? s.CreatedAtUtc)
            .Select(s => new
            {
                Session = s,
                ExpiresAtUtc = s.RefreshTokens
                    .Where(rt => rt.RevokedAtUtc == null && rt.ConsumedAtUtc == null)
                    .OrderByDescending(rt => rt.ExpiresAtUtc)
                    .Select(rt => (DateTime?)rt.ExpiresAtUtc)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var userIds = rows.Select(r => r.Session.UserId).Distinct().ToList();
        var users = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.Email, u.FirstName, u.LastName, u.Role })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var userMap = users.ToDictionary(u => u.Id, StringComparer.Ordinal);

        return rows.Select(r =>
        {
            userMap.TryGetValue(r.Session.UserId, out var u);
            var parsed = UserAgentParser.Parse(r.Session.UserAgent);
            var deviceName = parsed.DeviceName;
            if (string.IsNullOrEmpty(deviceName))
                deviceName = string.IsNullOrWhiteSpace(r.Session.ClientApp) ? null : r.Session.ClientApp.Trim();

            var displayName = u == null ? null : $"{u.FirstName} {u.LastName}".Trim();
            if (string.IsNullOrEmpty(displayName))
                displayName = u?.UserName;

            var dto = new ActiveSessionDto
            {
                Id = r.Session.Id,
                UserId = r.Session.UserId,
                ClientApp = r.Session.ClientApp,
                DeviceId = r.Session.DeviceId,
                DeviceName = deviceName,
                Browser = parsed.Browser,
                OS = parsed.OS,
                IpAddress = r.Session.IpAddress,
                UserAgent = r.Session.UserAgent,
                StartedAtUtc = r.Session.CreatedAtUtc,
                LastActivityAtUtc = r.Session.LastActivityAtUtc ?? r.Session.CreatedAtUtc,
                ExpiresAtUtc = r.ExpiresAtUtc,
                IsActive = !r.Session.RevokedAtUtc.HasValue,
                IsCurrent = currentSessionId.HasValue && r.Session.Id == currentSessionId.Value,
            };

            return AdminActiveSessionDtoMapper.From(
                dto,
                u?.UserName,
                u?.Email,
                displayName,
                u?.Role,
                r.Session.TenantId);
        }).ToList();
    }
}
