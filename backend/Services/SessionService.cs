using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services;

public interface ISessionService
{
    Task<IReadOnlyList<ActiveSession>> GetMyActiveSessionsAsync(string userId, Guid? currentSessionId, CancellationToken cancellationToken = default);

    Task<bool> TerminateSessionAsync(string userId, Guid sessionId, CancellationToken cancellationToken = default);

    Task<int> TerminateAllOtherSessionsAsync(string userId, Guid currentSessionId, CancellationToken cancellationToken = default);

    Task TouchSessionActivityAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

/// <summary>User session management backed by <c>auth_sessions</c>.</summary>
public sealed class SessionService : ISessionService
{
    private readonly IUserSessionService _inner;

    public SessionService(IUserSessionService inner)
    {
        _inner = inner;
    }

    public async Task<IReadOnlyList<ActiveSession>> GetMyActiveSessionsAsync(
        string userId,
        Guid? currentSessionId,
        CancellationToken cancellationToken = default)
    {
        var dtos = await _inner.GetActiveSessionsAsync(userId, currentSessionId, cancellationToken).ConfigureAwait(false);
        return dtos.Select(Map).ToList();
    }

    public Task<bool> TerminateSessionAsync(string userId, Guid sessionId, CancellationToken cancellationToken = default) =>
        _inner.TerminateSessionAsync(userId, sessionId, cancellationToken);

    public Task<int> TerminateAllOtherSessionsAsync(string userId, Guid currentSessionId, CancellationToken cancellationToken = default) =>
        _inner.TerminateOtherSessionsAsync(userId, currentSessionId, cancellationToken);

    public Task TouchSessionActivityAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _inner.TouchSessionActivityAsync(sessionId, cancellationToken);

    private static ActiveSession Map(ActiveSessionDto dto) => new()
    {
        Id = dto.Id,
        UserId = dto.UserId,
        ClientApp = dto.ClientApp,
        DeviceId = dto.DeviceId,
        DeviceName = dto.DeviceName,
        Browser = dto.Browser,
        OS = dto.OS,
        IpAddress = dto.IpAddress,
        UserAgent = dto.UserAgent,
        StartedAtUtc = dto.StartedAtUtc,
        LastActivityAtUtc = dto.LastActivityAtUtc,
        ExpiresAtUtc = dto.ExpiresAtUtc,
        IsActive = dto.IsActive,
        IsCurrent = dto.IsCurrent,
    };

    /// <summary>Maps to the product <see cref="UserSession"/> shape (CreatedAt / LastActiveAt naming).</summary>
    public static UserSession ToUserSession(ActiveSession session) => new()
    {
        Id = session.Id,
        UserId = session.UserId,
        ClientApp = session.ClientApp,
        DeviceId = session.DeviceId,
        DeviceName = session.DeviceName,
        Browser = session.Browser,
        OS = session.OS,
        IPAddress = session.IpAddress,
        UserAgent = session.UserAgent,
        CreatedAt = session.StartedAtUtc,
        LastActiveAt = session.LastActivityAtUtc,
        ExpiresAt = session.ExpiresAtUtc,
        IsActive = session.IsActive,
        IsCurrent = session.IsCurrent,
    };
}
