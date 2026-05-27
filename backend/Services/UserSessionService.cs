using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IUserSessionService
{
    Task<IReadOnlyList<ActiveSessionDto>> GetActiveSessionsAsync(string userId, Guid? currentSessionId, CancellationToken cancellationToken = default);

    Task<bool> TerminateSessionAsync(string userId, Guid sessionId, CancellationToken cancellationToken = default);

    Task<int> TerminateOtherSessionsAsync(string userId, Guid currentSessionId, CancellationToken cancellationToken = default);

    Task TouchSessionActivityAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

public sealed class UserSessionService : IUserSessionService
{
    private readonly AppDbContext _db;
    private readonly IRefreshTokenService _refreshTokens;

    public UserSessionService(AppDbContext db, IRefreshTokenService refreshTokens)
    {
        _db = db;
        _refreshTokens = refreshTokens;
    }

    public async Task<IReadOnlyList<ActiveSessionDto>> GetActiveSessionsAsync(
        string userId,
        Guid? currentSessionId,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _db.AuthSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAtUtc == null)
            .OrderByDescending(s => s.LastActivityAtUtc ?? s.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return sessions.Select(s => ToDto(s, currentSessionId)).ToList();
    }

    public async Task<bool> TerminateSessionAsync(string userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var exists = await _db.AuthSessions
            .AnyAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAtUtc == null, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            return false;

        await _refreshTokens.LogoutSessionAsync(sessionId, "user_terminated_session", cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<int> TerminateOtherSessionsAsync(
        string userId,
        Guid currentSessionId,
        CancellationToken cancellationToken = default)
    {
        var otherIds = await _db.AuthSessions
            .Where(s => s.UserId == userId && s.RevokedAtUtc == null && s.Id != currentSessionId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var id in otherIds)
            await _refreshTokens.LogoutSessionAsync(id, "user_terminate_all_other", cancellationToken).ConfigureAwait(false);

        return otherIds.Count;
    }

    public async Task TouchSessionActivityAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        if (_db.Database.IsRelational())
        {
            await _db.AuthSessions
                .Where(s => s.Id == sessionId && s.RevokedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(s => s.LastActivityAtUtc, now),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var session = await _db.AuthSessions.SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken).ConfigureAwait(false);
        if (session == null || session.RevokedAtUtc.HasValue)
            return;
        session.LastActivityAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ActiveSessionDto ToDto(AuthSession s, Guid? currentSessionId) => new()
    {
        Id = s.Id,
        UserId = s.UserId,
        ClientApp = s.ClientApp,
        DeviceId = s.DeviceId,
        IpAddress = s.IpAddress,
        StartedAtUtc = s.CreatedAtUtc,
        LastActivityAtUtc = s.LastActivityAtUtc ?? s.CreatedAtUtc,
        IsActive = !s.RevokedAtUtc.HasValue,
        IsCurrent = currentSessionId.HasValue && s.Id == currentSessionId.Value,
    };
}
