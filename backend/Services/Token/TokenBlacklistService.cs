using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace KasseAPI_Final.Services.Token;

public interface ITokenBlacklistService
{
    /// <summary>
    /// Marks an access token as revoked until <paramref name="expiry"/> (UTC).
    /// Entries expire from cache at the same time as the JWT so memory stays bounded.
    /// </summary>
    void BlacklistToken(string token, DateTime expiry);

    /// <summary>True when the access token was revoked via logout (or equivalent).</summary>
    bool IsTokenBlacklisted(string token);
}

/// <summary>
/// In-memory JWT access-token blacklist for immediate logout invalidation.
/// Complements DB session revocation (<see cref="IRefreshTokenService"/>); does not replace it.
/// Tokens are stored as SHA-256 digests — never log or cache raw JWTs.
/// </summary>
public sealed class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<TokenBlacklistService> _logger;

    public TokenBlacklistService(
        IMemoryCache cache,
        ILogger<TokenBlacklistService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public void BlacklistToken(string token, DateTime expiry)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        var key = BuildCacheKey(token);
        var expiresAtUtc = expiry.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(expiry, DateTimeKind.Utc)
            : expiry.ToUniversalTime();

        var ttl = expiresAtUtc - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            // Already expired — no cache entry needed; JWT lifetime validation will reject it.
            return;
        }

        _cache.Set(key, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
        });

        _logger.LogInformation(
            "Access token blacklisted until {ExpiresAtUtc:o} (digest={DigestPrefix})",
            expiresAtUtc,
            key.Length >= 28 ? key[16..28] : "n/a");
    }

    public bool IsTokenBlacklisted(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        return _cache.TryGetValue(BuildCacheKey(token), out _);
    }

    private static string BuildCacheKey(string token)
    {
        var normalized = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? token[7..].Trim()
            : token.Trim();

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return "token_blacklist:" + Convert.ToHexString(hash);
    }
}
