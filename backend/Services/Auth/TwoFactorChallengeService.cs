using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace KasseAPI_Final.Services.Auth;

/// <summary>
/// In-memory pending 2FA challenges. Suitable for single-instance / sticky sessions;
/// multi-node deployments should replace with distributed cache.
/// </summary>
public sealed class TwoFactorChallengeService : ITwoFactorChallengeService
{
    private const string CacheKeyPrefix = "auth:2fa-challenge:";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;

    public TwoFactorChallengeService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string CreateChallenge(TwoFactorChallengePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (string.IsNullOrWhiteSpace(payload.UserId))
            throw new ArgumentException("UserId is required.", nameof(payload));

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var ttl = payload.ExpiresAtUtc > DateTime.UtcNow
            ? payload.ExpiresAtUtc - DateTime.UtcNow
            : DefaultTtl;
        if (ttl < TimeSpan.FromSeconds(30))
            ttl = TimeSpan.FromSeconds(30);
        if (ttl > DefaultTtl)
            ttl = DefaultTtl;

        _cache.Set(CacheKeyPrefix + token, payload, ttl);
        return token;
    }

    public bool TryConsumeChallenge(string token, out TwoFactorChallengePayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var key = CacheKeyPrefix + token.Trim();
        if (!_cache.TryGetValue(key, out TwoFactorChallengePayload? stored) || stored is null)
            return false;

        _cache.Remove(key);

        if (stored.ExpiresAtUtc <= DateTime.UtcNow)
            return false;

        payload = stored;
        return true;
    }
}
