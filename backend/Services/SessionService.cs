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
        IpAddress = dto.IpAddress,
        StartedAtUtc = dto.StartedAtUtc,
        LastActivityAtUtc = dto.LastActivityAtUtc,
        IsActive = dto.IsActive,
        IsCurrent = dto.IsCurrent,
    };
}
