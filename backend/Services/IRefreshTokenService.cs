namespace KasseAPI_Final.Services;

public sealed record IssuedTokenPair(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid SessionId,
    string AccessJti);

public sealed record RefreshResult(
    bool Success,
    bool ReuseDetected,
    IssuedTokenPair? Tokens,
    string ErrorCode);

public interface IRefreshTokenService
{
    /// <param name="buildAccessToken">Last string? is persisted session tenant id (canonical GUID) when known.</param>
    /// <param name="sessionTenantId">Stored on <c>auth_sessions</c> for refresh rotation (single-tenant: default tenant).</param>
    Task<IssuedTokenPair> IssueLoginTokensAsync(
        string userId,
        string clientApp,
        Func<string, string, Guid, DateTime, string, string?, Task<string>> buildAccessToken,
        Guid? sessionTenantId = null,
        CancellationToken cancellationToken = default);

    Task<RefreshResult> RotateAsync(
        string refreshToken,
        Func<string, string, Guid, DateTime, string, string?, Task<string>> buildAccessToken,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeRefreshTokenAsync(string refreshToken, string reason, CancellationToken cancellationToken = default);

    Task LogoutSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken = default);

    Task LogoutAllAsync(string userId, string reason, CancellationToken cancellationToken = default);

    Task<bool> IsSessionActiveAsync(string userId, Guid sessionId, CancellationToken cancellationToken = default);
}
