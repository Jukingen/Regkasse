using System.Security.Cryptography;
using System.Text;
using KasseAPI_Final.Configuration;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Security;

public interface ICsrfTokenService
{
    string GenerateToken();
    bool ValidateToken(string token, string cookieToken);
}

/// <summary>
/// Issues and validates CSRF tokens (double-submit: header value must equal cookie value,
/// and the token must still be present in the server-side cache).
/// </summary>
public sealed class CsrfTokenService : ICsrfTokenService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CsrfTokenService> _logger;
    private readonly IOptionsMonitor<CsrfOptions> _options;

    public CsrfTokenService(
        IMemoryCache cache,
        ILogger<CsrfTokenService> logger,
        IOptionsMonitor<CsrfOptions> options)
    {
        _cache = cache;
        _logger = logger;
        _options = options;
    }

    public string GenerateToken()
    {
        // Base64Url avoids '+'/'/' which break Cookie header parsing.
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var hours = Math.Clamp(_options.CurrentValue.TokenLifetimeHours, 1, 168);
        var ttl = TimeSpan.FromHours(hours);

        _cache.Set(
            CacheKey(token),
            true,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });

        _logger.LogDebug("CSRF token generated");
        return token;
    }

    public bool ValidateToken(string token, string cookieToken)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(cookieToken))
        {
            _logger.LogWarning("CSRF validation failed: missing token");
            return false;
        }

        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var cookieBytes = Encoding.UTF8.GetBytes(cookieToken);
        if (tokenBytes.Length != cookieBytes.Length
            || !CryptographicOperations.FixedTimeEquals(tokenBytes, cookieBytes))
        {
            _logger.LogWarning("CSRF validation failed: token mismatch");
            return false;
        }

        if (!_cache.TryGetValue(CacheKey(token), out _))
        {
            _logger.LogWarning("CSRF validation failed: token expired");
            return false;
        }

        return true;
    }

    private static string CacheKey(string token) => $"csrf_{token}";
}
