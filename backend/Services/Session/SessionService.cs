using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.Token;

namespace KasseAPI_Final.Services.Session;

/// <summary>
/// Sketch-aligned device session API over <c>auth_sessions</c>.
/// No <c>UserSessions</c> table and no stored access-token plaintext.
/// </summary>
public interface IDeviceSessionService
{
    Task<IReadOnlyList<UserSessionDto>> GetActiveSessionsAsync(
        string userId,
        Guid? currentSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes session refresh tokens. When revoking the caller's current session,
    /// also blacklists <paramref name="currentAccessToken"/>.
    /// </summary>
    Task<bool> RevokeSessionAsync(
        string userId,
        Guid sessionId,
        Guid? currentSessionId = null,
        string? currentAccessToken = null,
        DateTime? currentAccessTokenExpiresAtUtc = null,
        CancellationToken cancellationToken = default);

    Task<int> RevokeOtherSessionsAsync(
        string userId,
        Guid currentSessionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Sketch path <c>Services/Session/SessionService.cs</c>; type name <see cref="DeviceSessionService"/>
/// avoids clash with API facade <see cref="Services.SessionService"/>.
/// </summary>
public sealed class DeviceSessionService : IDeviceSessionService
{
    private readonly IUserSessionService _sessions;
    private readonly ITokenBlacklistService _tokenBlacklist;

    public DeviceSessionService(IUserSessionService sessions, ITokenBlacklistService tokenBlacklist)
    {
        _sessions = sessions;
        _tokenBlacklist = tokenBlacklist;
    }

    public async Task<IReadOnlyList<UserSessionDto>> GetActiveSessionsAsync(
        string userId,
        Guid? currentSessionId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _sessions
            .GetActiveSessionsAsync(userId, currentSessionId, cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToUserSessionDto).ToList();
    }

    public async Task<bool> RevokeSessionAsync(
        string userId,
        Guid sessionId,
        Guid? currentSessionId = null,
        string? currentAccessToken = null,
        DateTime? currentAccessTokenExpiresAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        var ok = await _sessions
            .TerminateSessionAsync(userId, sessionId, cancellationToken)
            .ConfigureAwait(false);
        if (!ok)
            return false;

        if (currentSessionId.HasValue
            && currentSessionId.Value == sessionId
            && !string.IsNullOrWhiteSpace(currentAccessToken))
        {
            var expiry = currentAccessTokenExpiresAtUtc ?? DateTime.UtcNow.AddHours(24);
            _tokenBlacklist.BlacklistToken(currentAccessToken, expiry);
        }

        return true;
    }

    public Task<int> RevokeOtherSessionsAsync(
        string userId,
        Guid currentSessionId,
        CancellationToken cancellationToken = default) =>
        _sessions.TerminateOtherSessionsAsync(userId, currentSessionId, cancellationToken);

    public static DateTime? TryGetAccessTokenExpiry(ClaimsPrincipal user)
    {
        var expRaw = user.FindFirst(JwtRegisteredClaimNames.Exp)?.Value
            ?? user.FindFirst("exp")?.Value;
        if (long.TryParse(expRaw, out var expUnix))
            return DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
        return null;
    }

    public static string? TryGetBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return null;
        var header = authorizationHeader.Trim();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return header[7..].Trim();
        return header;
    }

    private static UserSessionDto ToUserSessionDto(ActiveSessionDto s) => new()
    {
        Id = s.Id,
        DeviceName = s.DeviceName,
        Browser = s.Browser,
        OS = s.OS,
        IPAddress = s.IpAddress,
        LastActiveAt = s.LastActivityAtUtc,
        CreatedAt = s.StartedAtUtc,
        ExpiresAt = s.ExpiresAtUtc,
        IsCurrent = s.IsCurrent,
        IsActive = s.IsActive,
        ClientApp = s.ClientApp,
    };
}
