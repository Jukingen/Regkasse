using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>
/// Admin session management: list/revoke <c>auth_sessions</c> (including POS), revoke refresh tokens,
/// and invalidate Identity <c>SecurityStamp</c> so existing JWTs fail on the next request.
/// Manager callers are tenant-scoped; Super Admin may see all tenants.
/// </summary>
public interface ISessionManagementService
{
    Task<IReadOnlyList<AdminActiveSessionDto>> GetActiveSessionsAsync(
        Guid? currentSessionId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminActiveSessionDto>> ListSessionsAsync(
        SessionManagementAccess access,
        AdminSessionListFilter? filter = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminActiveSessionDto>> GetUserSessionsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminActiveSessionDto>> GetUserSessionsAsync(
        string userId,
        SessionManagementAccess access,
        CancellationToken cancellationToken = default);

    Task<AdminSessionTerminateResult> TerminateSessionAsync(
        Guid sessionId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default,
        Guid? currentSessionId = null,
        Guid? scopedTenantId = null);

    Task<int> TerminateBulkAsync(
        IReadOnlyList<Guid> sessionIds,
        string actorUserId,
        string actorRole,
        Guid? exceptSessionId,
        Guid? scopedTenantId,
        CancellationToken cancellationToken = default);

    Task<int> TerminateAllUserSessionsAsync(
        string userId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes every in-scope active session except <paramref name="exceptSessionId"/> (caller's current session).</summary>
    Task<int> TerminateAllSessionsAsync(
        string actorUserId,
        string actorRole,
        Guid? exceptSessionId,
        CancellationToken cancellationToken = default,
        Guid? scopedTenantId = null);

    Task<bool> ForceLogoutAsync(
        string userId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Request-path check used from JWT <c>OnTokenValidated</c>:
    /// user exists and is active, optional security-stamp match, optional session not revoked.
    /// </summary>
    Task<bool> IsSessionValidAsync(
        string userId,
        Guid? sessionId,
        string? securityStamp,
        CancellationToken cancellationToken = default);
}
