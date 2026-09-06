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
    public const string RevokeReasonTerminateBulk = "admin_terminate_bulk";

    /// <summary>How far back expired/revoked sessions are listed (keeps the admin list bounded).</summary>
    public static readonly TimeSpan ExpiredLookback = TimeSpan.FromDays(30);

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
        QuerySessionsAsync(
            new SessionManagementAccess
            {
                ActorUserId = string.Empty,
                ActorRole = string.Empty,
                IsSuperAdmin = true,
                CurrentSessionId = currentSessionId,
            },
            new AdminSessionListFilter { Status = AdminSessionListFilter.StatusActive },
            cancellationToken);

    public Task<IReadOnlyList<AdminActiveSessionDto>> ListSessionsAsync(
        SessionManagementAccess access,
        AdminSessionListFilter? filter = null,
        CancellationToken cancellationToken = default) =>
        QuerySessionsAsync(access, filter ?? new AdminSessionListFilter(), cancellationToken);

    public Task<IReadOnlyList<AdminActiveSessionDto>> GetUserSessionsAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        GetUserSessionsAsync(
            userId,
            new SessionManagementAccess
            {
                ActorUserId = string.Empty,
                ActorRole = string.Empty,
                IsSuperAdmin = true,
            },
            cancellationToken);

    public Task<IReadOnlyList<AdminActiveSessionDto>> GetUserSessionsAsync(
        string userId,
        SessionManagementAccess access,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Task.FromResult<IReadOnlyList<AdminActiveSessionDto>>(Array.Empty<AdminActiveSessionDto>());

        return QuerySessionsAsync(
            access,
            new AdminSessionListFilter
            {
                UserId = userId,
                Status = AdminSessionListFilter.StatusActive,
            },
            cancellationToken);
    }

    public async Task<AdminSessionTerminateResult> TerminateSessionAsync(
        Guid sessionId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default,
        Guid? currentSessionId = null,
        Guid? scopedTenantId = null)
    {
        if (currentSessionId.HasValue && currentSessionId.Value == sessionId)
            return AdminSessionTerminateResult.CurrentSession();

        var session = await _db.AuthSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == sessionId && s.RevokedAtUtc == null, cancellationToken)
            .ConfigureAwait(false);
        if (session == null || !IsSessionInScope(session.TenantId, scopedTenantId))
            return AdminSessionTerminateResult.NotFound();

        await _refreshTokens.LogoutSessionAsync(sessionId, RevokeReasonTerminateSession, cancellationToken)
            .ConfigureAwait(false);

        await _auditLog.LogUserLifecycleAsync(
            AuditEventType.UserSessionTerminated,
            actorUserId,
            actorRole,
            session.UserId,
            reason: RevokeReasonTerminateSession,
            description: $"Session {sessionId:D} terminated",
            newValues: new { sessionId, clientApp = session.ClientApp, tenantId = session.TenantId })
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Admin {ActorUserId} ({ActorRole}) terminated session {SessionId} for user {TargetUserId} clientApp={ClientApp}",
            actorUserId,
            actorRole,
            sessionId,
            session.UserId,
            session.ClientApp);
        return AdminSessionTerminateResult.Ok();
    }

    public async Task<int> TerminateBulkAsync(
        IReadOnlyList<Guid> sessionIds,
        string actorUserId,
        string actorRole,
        Guid? exceptSessionId,
        Guid? scopedTenantId,
        CancellationToken cancellationToken = default)
    {
        if (sessionIds == null || sessionIds.Count == 0)
            return 0;

        var uniqueIds = sessionIds.Distinct().ToList();
        var sessions = await _db.AuthSessions
            .AsNoTracking()
            .Where(s => uniqueIds.Contains(s.Id) && s.RevokedAtUtc == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var toRevoke = sessions
            .Where(s => IsSessionInScope(s.TenantId, scopedTenantId)
                && (!exceptSessionId.HasValue || s.Id != exceptSessionId.Value))
            .ToList();

        foreach (var session in toRevoke)
        {
            await _refreshTokens.LogoutSessionAsync(session.Id, RevokeReasonTerminateBulk, cancellationToken)
                .ConfigureAwait(false);
        }

        if (toRevoke.Count > 0)
        {
            await _auditLog.LogSystemOperationAsync(
                AuditLogActions.USER_SESSION_TERMINATED,
                entityType: AuditLogEntityTypes.USER,
                userId: actorUserId,
                userRole: actorRole,
                description: $"Terminated {toRevoke.Count} session(s) (bulk)",
                notes: exceptSessionId.HasValue ? $"exceptSessionId={exceptSessionId.Value:D}" : null,
                requestData: new
                {
                    requestedCount = uniqueIds.Count,
                    scopedTenantId,
                    targetUserIds = toRevoke.Select(s => s.UserId).Distinct().ToArray(),
                    sessionIds = toRevoke.Select(s => s.Id).ToArray(),
                },
                responseData: new { terminatedCount = toRevoke.Count },
                actionType: AuditEventType.UserSessionTerminated).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Admin {ActorUserId} ({ActorRole}) bulk-terminated {Count} session(s)",
            actorUserId,
            actorRole,
            toRevoke.Count);
        return toRevoke.Count;
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
        CancellationToken cancellationToken = default,
        Guid? scopedTenantId = null)
    {
        var query = _db.AuthSessions.Where(s =>
            s.RevokedAtUtc == null && (exceptSessionId == null || s.Id != exceptSessionId.Value));
        if (scopedTenantId.HasValue)
            query = query.Where(s => s.TenantId == scopedTenantId.Value);

        var sessionIds = await query.Select(s => s.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var id in sessionIds)
            await _refreshTokens.LogoutSessionAsync(id, RevokeReasonTerminateAll, cancellationToken).ConfigureAwait(false);

        var scopeLabel = scopedTenantId.HasValue ? "tenant" : "platform";
        await _auditLog.LogSystemOperationAsync(
            AuditLogActions.USER_SESSION_TERMINATED,
            entityType: AuditLogEntityTypes.USER,
            userId: actorUserId,
            userRole: actorRole,
            description: $"Terminated {sessionIds.Count} session(s) ({scopeLabel})",
            notes: exceptSessionId.HasValue ? $"exceptSessionId={exceptSessionId.Value:D}" : null,
            requestData: new { exceptSessionId, scopedTenantId },
            responseData: new { terminatedCount = sessionIds.Count },
            actionType: AuditEventType.UserSessionTerminated).ConfigureAwait(false);

        _logger.LogWarning(
            "Admin {ActorUserId} ({ActorRole}) terminated {Count} {Scope} session(s) except {ExceptSessionId}",
            actorUserId,
            actorRole,
            sessionIds.Count,
            scopeLabel,
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

    private async Task<IReadOnlyList<AdminActiveSessionDto>> QuerySessionsAsync(
        SessionManagementAccess access,
        AdminSessionListFilter filter,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var status = NormalizeStatus(filter.Status);
        var includeRevoked = status is AdminSessionListFilter.StatusExpired or AdminSessionListFilter.StatusAll;
        var cutoff = now - ExpiredLookback;

        var query = _db.AuthSessions.AsNoTracking().AsQueryable();
        if (!includeRevoked)
            query = query.Where(s => s.RevokedAtUtc == null);
        else
            query = query.Where(s =>
                s.RevokedAtUtc == null
                || s.RevokedAtUtc >= cutoff
                || (s.LastActivityAtUtc != null && s.LastActivityAtUtc >= cutoff)
                || s.CreatedAtUtc >= cutoff);

        var tenantScope = access.ScopedTenantId ?? filter.TenantId;
        if (tenantScope.HasValue)
            query = query.Where(s => s.TenantId == tenantScope.Value);

        if (!string.IsNullOrWhiteSpace(filter.UserId))
            query = query.Where(s => s.UserId == filter.UserId);

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

        var tenantIds = rows
            .Select(r => r.Session.TenantId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var tenantMap = tenantIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Tenants
                .AsNoTracking()
                .Where(t => tenantIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name })
                .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken)
                .ConfigureAwait(false);

        var search = filter.Search?.Trim();
        var roleFilter = filter.Role?.Trim();

        var mapped = rows.Select(r =>
        {
            userMap.TryGetValue(r.Session.UserId, out var u);
            var parsed = UserAgentParser.Parse(r.Session.UserAgent);
            var deviceName = parsed.DeviceName;
            if (string.IsNullOrEmpty(deviceName))
                deviceName = string.IsNullOrWhiteSpace(r.Session.ClientApp) ? null : r.Session.ClientApp.Trim();

            var displayName = u == null ? null : $"{u.FirstName} {u.LastName}".Trim();
            if (string.IsNullOrEmpty(displayName))
                displayName = u?.UserName;

            var revoked = r.Session.RevokedAtUtc.HasValue;
            var tokenExpired = r.ExpiresAtUtc.HasValue && r.ExpiresAtUtc.Value <= now;
            var isActive = !revoked && !tokenExpired;
            var started = r.Session.CreatedAtUtc;
            var lastActivity = r.Session.LastActivityAtUtc ?? started;
            var ended = r.Session.RevokedAtUtc
                ?? (tokenExpired ? r.ExpiresAtUtc : null)
                ?? now;
            if (ended < started)
                ended = started;
            var durationSeconds = (int)Math.Max(0, (ended - started).TotalSeconds);
            var platformLabel = SessionPlatformLabel.Resolve(r.Session.ClientApp, parsed.OS);

            string? tenantName = null;
            if (r.Session.TenantId is Guid tid)
                tenantMap.TryGetValue(tid, out tenantName);

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
                StartedAtUtc = started,
                LastActivityAtUtc = lastActivity,
                ExpiresAtUtc = r.ExpiresAtUtc,
                IsActive = isActive,
                IsCurrent = access.CurrentSessionId.HasValue && r.Session.Id == access.CurrentSessionId.Value,
            };

            return AdminActiveSessionDtoMapper.From(
                dto,
                u?.UserName,
                u?.Email,
                displayName,
                u?.Role,
                r.Session.TenantId,
                tenantName,
                platformLabel,
                durationSeconds);
        });

        IEnumerable<AdminActiveSessionDto> result = mapped;

        if (status == AdminSessionListFilter.StatusActive)
            result = result.Where(s => s.IsActive);
        else if (status == AdminSessionListFilter.StatusExpired)
            result = result.Where(s => !s.IsActive);

        if (!string.IsNullOrEmpty(roleFilter))
            result = result.Where(s =>
                string.Equals(s.Role, roleFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(search))
        {
            result = result.Where(s =>
                ContainsIgnoreCase(s.DisplayName, search)
                || ContainsIgnoreCase(s.UserName, search)
                || ContainsIgnoreCase(s.Email, search)
                || ContainsIgnoreCase(s.UserId, search)
                || ContainsIgnoreCase(s.IpAddress, search)
                || ContainsIgnoreCase(s.DeviceName, search)
                || ContainsIgnoreCase(s.PlatformLabel, search)
                || ContainsIgnoreCase(s.TenantName, search));
        }

        return result.ToList();
    }

    private static bool IsSessionInScope(Guid? sessionTenantId, Guid? scopedTenantId)
    {
        if (!scopedTenantId.HasValue)
            return true;
        return sessionTenantId.HasValue && sessionTenantId.Value == scopedTenantId.Value;
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.Equals(status, AdminSessionListFilter.StatusExpired, StringComparison.OrdinalIgnoreCase))
            return AdminSessionListFilter.StatusExpired;
        if (string.Equals(status, AdminSessionListFilter.StatusAll, StringComparison.OrdinalIgnoreCase))
            return AdminSessionListFilter.StatusAll;
        return AdminSessionListFilter.StatusActive;
    }

    private static bool ContainsIgnoreCase(string? value, string search) =>
        !string.IsNullOrEmpty(value)
        && value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
