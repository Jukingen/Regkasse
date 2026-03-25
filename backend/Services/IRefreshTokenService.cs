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
    Task<IssuedTokenPair> IssueLoginTokensAsync(
        string userId,
        string clientApp,
        Func<string, string, Guid, DateTime, string, Task<string>> buildAccessToken,
        CancellationToken cancellationToken = default);

    Task<RefreshResult> RotateAsync(
        string refreshToken,
        Func<string, string, Guid, DateTime, string, Task<string>> buildAccessToken,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeRefreshTokenAsync(string refreshToken, string reason, CancellationToken cancellationToken = default);

    Task LogoutSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken = default);

    Task LogoutAllAsync(string userId, string reason, CancellationToken cancellationToken = default);

    Task<bool> IsSessionActiveAsync(string userId, Guid sessionId, CancellationToken cancellationToken = default);
}
