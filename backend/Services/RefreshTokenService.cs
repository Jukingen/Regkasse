using System.Security.Cryptography;
using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KasseAPI_Final.Services;

public sealed class RefreshTokenService : IRefreshTokenService, IUserSessionInvalidation
{
    private const string MissingAuthSchemaMessage =
        "Critical auth schema is missing (auth_sessions/refresh_tokens). Apply migration 'AddAuthSessionsAndRefreshTokens' to the active runtime database.";

    private readonly AppDbContext _db;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(AppDbContext db, IOptions<AuthOptions> authOptions, ILogger<RefreshTokenService> logger)
    {
        _db = db;
        _authOptions = authOptions.Value;
        _logger = logger;
    }

    public async Task<IssuedTokenPair> IssueLoginTokensAsync(
        string userId,
        string clientApp,
        Func<string, string, Guid, DateTime, string, string?, Task<string>> buildAccessToken,
        Guid? sessionTenantId = null,
        SessionClientMetadata? clientMetadata = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var session = new AuthSession
        {
            UserId = userId,
            ClientApp = clientApp,
            CreatedAtUtc = now,
            LastActivityAtUtc = now,
            TenantId = sessionTenantId,
            DeviceId = Truncate(clientMetadata?.DeviceId, 200),
            IpAddress = Truncate(clientMetadata?.IpAddress, 45),
            UserAgent = Truncate(clientMetadata?.UserAgent, 500),
        };

        var accessJti = Guid.NewGuid().ToString("N");
        var accessExpiresAt = DateTime.UtcNow.AddMinutes(_authOptions.AccessTokenLifetimeMinutes);
        var refreshTokenPlain = GenerateOpaqueToken();
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_authOptions.RefreshTokenLifetimeDays);

        var refreshToken = new RefreshToken
        {
            UserId = userId,
            SessionId = session.Id,
            TokenHash = HashToken(refreshTokenPlain),
            AccessJti = accessJti,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = refreshExpiresAt
        };

        try
        {
            _db.AuthSessions.Add(session);
            _db.RefreshTokens.Add(refreshToken);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsMissingAuthSchemaException(ex))
        {
            throw new InvalidOperationException(MissingAuthSchemaMessage, ex);
        }

        var persistedTenant = session.TenantId?.ToString("D");
        var accessToken = await buildAccessToken(userId, accessJti, session.Id, accessExpiresAt, clientApp, persistedTenant);
        return new IssuedTokenPair(accessToken, accessExpiresAt, refreshTokenPlain, refreshExpiresAt, session.Id, accessJti);
    }

    public async Task<RefreshResult> RotateAsync(
        string refreshToken,
        Func<string, string, Guid, DateTime, string, string?, Task<string>> buildAccessToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        var token = await _db.RefreshTokens
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (token == null)
            return new RefreshResult(false, false, null, "refresh_token_invalid");

        var session = await _db.AuthSessions.SingleOrDefaultAsync(x => x.Id == token.SessionId, cancellationToken);
        if (session == null)
            return new RefreshResult(false, false, null, "session_not_found");

        if (session.RevokedAtUtc.HasValue)
            return new RefreshResult(false, false, null, "session_revoked");

        if (token.RevokedAtUtc.HasValue || token.ConsumedAtUtc.HasValue || token.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await LogoutSessionAsync(session.Id, "refresh_token_reuse_detected", cancellationToken);
            _logger.LogWarning("Refresh token reuse detected for user {UserId}, session {SessionId}", token.UserId, token.SessionId);
            return new RefreshResult(false, true, null, "refresh_token_reuse_detected");
        }

        var consumedAt = DateTime.UtcNow;
        var updated = await MarkTokenConsumedAsync(token.Id, consumedAt, cancellationToken);

        if (updated == 0)
        {
            await LogoutSessionAsync(session.Id, "refresh_token_parallel_reuse_detected", cancellationToken);
            return new RefreshResult(false, true, null, "refresh_token_reuse_detected");
        }

        var accessJti = Guid.NewGuid().ToString("N");
        var accessExpiresAt = DateTime.UtcNow.AddMinutes(_authOptions.AccessTokenLifetimeMinutes);
        var refreshTokenPlain = GenerateOpaqueToken();
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_authOptions.RefreshTokenLifetimeDays);

        var successor = new RefreshToken
        {
            UserId = token.UserId,
            SessionId = token.SessionId,
            TokenHash = HashToken(refreshTokenPlain),
            AccessJti = accessJti,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = refreshExpiresAt
        };

        try
        {
            _db.RefreshTokens.Add(successor);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsMissingAuthSchemaException(ex))
        {
            throw new InvalidOperationException(MissingAuthSchemaMessage, ex);
        }

        await SetReplacedByTokenAsync(token.Id, successor.Id, cancellationToken);

        var persistedTenant = session.TenantId?.ToString("D");
        var accessToken = await buildAccessToken(token.UserId, accessJti, token.SessionId, accessExpiresAt, session.ClientApp, persistedTenant);
        var tokens = new IssuedTokenPair(accessToken, accessExpiresAt, refreshTokenPlain, refreshExpiresAt, token.SessionId, accessJti);
        return new RefreshResult(true, false, tokens, string.Empty);
    }

    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken, string reason, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        var token = await _db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (token == null)
            return false;

        await LogoutSessionAsync(token.SessionId, reason, cancellationToken);
        return true;
    }

    public async Task LogoutSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await RevokeSessionAndTokensAsync(sessionId, now, reason, cancellationToken);
    }

    public async Task LogoutAllAsync(string userId, string reason, CancellationToken cancellationToken = default)
    {
        var sessionIds = await _db.AuthSessions
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var sessionId in sessionIds)
            await LogoutSessionAsync(sessionId, reason, cancellationToken);
    }

    public Task InvalidateSessionsForUserAsync(string userId, CancellationToken cancellationToken = default) =>
        LogoutAllAsync(userId, "user_lifecycle_invalidation", cancellationToken);

    public async Task<bool> IsSessionActiveAsync(string userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _db.AuthSessions
            .AnyAsync(x => x.Id == sessionId && x.UserId == userId && x.RevokedAtUtc == null, cancellationToken);
    }

    private static string GenerateOpaqueToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value.Trim() : value.Trim()[..max];

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private async Task<int> MarkTokenConsumedAsync(Guid tokenId, DateTime consumedAt, CancellationToken cancellationToken)
    {
        if (_db.Database.IsRelational())
        {
            return await _db.RefreshTokens
                .Where(x => x.Id == tokenId && x.ConsumedAtUtc == null && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ConsumedAtUtc, consumedAt), cancellationToken);
        }

        var token = await _db.RefreshTokens.SingleOrDefaultAsync(x => x.Id == tokenId, cancellationToken);
        if (token == null || token.ConsumedAtUtc.HasValue || token.RevokedAtUtc.HasValue)
            return 0;
        token.ConsumedAtUtc = consumedAt;
        await _db.SaveChangesAsync(cancellationToken);
        return 1;
    }

    private async Task SetReplacedByTokenAsync(Guid tokenId, Guid replacedByTokenId, CancellationToken cancellationToken)
    {
        if (_db.Database.IsRelational())
        {
            await _db.RefreshTokens
                .Where(x => x.Id == tokenId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReplacedByTokenId, replacedByTokenId), cancellationToken);
            return;
        }

        var token = await _db.RefreshTokens.SingleAsync(x => x.Id == tokenId, cancellationToken);
        token.ReplacedByTokenId = replacedByTokenId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeSessionAndTokensAsync(Guid sessionId, DateTime now, string reason, CancellationToken cancellationToken)
    {
        if (_db.Database.IsRelational())
        {
            await _db.AuthSessions
                .Where(x => x.Id == sessionId && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.RevokedAtUtc, now)
                    .SetProperty(x => x.RevokedReason, reason), cancellationToken);

            await _db.RefreshTokens
                .Where(x => x.SessionId == sessionId && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.RevokedAtUtc, now)
                    .SetProperty(x => x.RevokeReason, reason), cancellationToken);
            return;
        }

        var session = await _db.AuthSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session != null && !session.RevokedAtUtc.HasValue)
        {
            session.RevokedAtUtc = now;
            session.RevokedReason = reason;
        }

        var tokens = await _db.RefreshTokens.Where(x => x.SessionId == sessionId && x.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.RevokedAtUtc = now;
            token.RevokeReason = reason;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsMissingAuthSchemaException(Exception ex)
    {
        if (ex is PostgresException pg && pg.SqlState == PostgresErrorCodes.UndefinedTable)
            return true;

        if (ex is DbUpdateException dbUpdate && dbUpdate.InnerException is not null)
        {
            var text = dbUpdate.InnerException.ToString();
            if (text.Contains("42P01", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("no such table", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("relation \"auth_sessions\" does not exist", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("relation \"refresh_tokens\" does not exist", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
