using System.Threading;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Auth;

public interface IAccountLockoutService
{
    bool IsLockedOut(string loginIdentifier);

    void RecordFailedAttempt(string loginIdentifier);

    void ResetAttempts(string loginIdentifier);
}

/// <summary>
/// Temporary login lockout by login identifier (email or username), stored in <see cref="IMemoryCache"/>.
/// Case-insensitive keys. Thread-safe attempt counter.
/// </summary>
public sealed class AccountLockoutService : IAccountLockoutService
{
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<AccountLockoutOptions> _options;

    public AccountLockoutService(IMemoryCache cache, IOptionsMonitor<AccountLockoutOptions> options)
    {
        _cache = cache;
        _options = options;
    }

    public bool IsLockedOut(string loginIdentifier)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
            return false;

        var key = NormalizeKey(loginIdentifier);
        if (key is null)
            return false;

        return _cache.TryGetValue(LockoutCacheKey(key), out _);
    }

    public void RecordFailedAttempt(string loginIdentifier)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled || options.MaxAttempts <= 0 || options.LockoutMinutes <= 0)
            return;

        var key = NormalizeKey(loginIdentifier);
        if (key is null)
            return;

        var window = TimeSpan.FromMinutes(options.LockoutMinutes);
        var attemptsKey = AttemptsCacheKey(key);
        var counter = _cache.GetOrCreate(attemptsKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = window;
            return new AttemptCounter();
        })!;

        var attempts = counter.Increment();
        if (attempts >= options.MaxAttempts)
        {
            _cache.Set(LockoutCacheKey(key), true, window);
        }
    }

    public void ResetAttempts(string loginIdentifier)
    {
        var key = NormalizeKey(loginIdentifier);
        if (key is null)
            return;

        _cache.Remove(AttemptsCacheKey(key));
        _cache.Remove(LockoutCacheKey(key));
    }

    private static string? NormalizeKey(string? loginIdentifier)
    {
        if (string.IsNullOrWhiteSpace(loginIdentifier))
            return null;
        return loginIdentifier.Trim().ToUpperInvariant();
    }

    private static string AttemptsCacheKey(string normalizedKey) => $"auth:attempts:{normalizedKey}";

    private static string LockoutCacheKey(string normalizedKey) => $"auth:lockout:{normalizedKey}";

    private sealed class AttemptCounter
    {
        private int _count;

        public int Increment() => Interlocked.Increment(ref _count);
    }
}
