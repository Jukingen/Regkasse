using System.Collections.Concurrent;
using KasseAPI_Final.Services.Metrics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Cache;

/// <summary>
/// <see cref="IMemoryCache"/>-backed <see cref="ICacheService"/>.
/// Tracks keys so <see cref="RemoveByPrefixAsync"/> works without Redis.
/// </summary>
public sealed class MemoryCacheService : ICacheService
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly ICacheMetricsService _metrics;
    private readonly ConcurrentDictionary<string, byte> _knownKeys = new(StringComparer.Ordinal);

    public MemoryCacheService(
        IMemoryCache cache,
        ILogger<MemoryCacheService> logger,
        ICacheMetricsService metrics)
    {
        _cache = cache;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        var created = false;
        var result = await _cache.GetOrCreateAsync(key, async entry =>
        {
            created = true;
            entry.AbsoluteExpirationRelativeToNow = expiry ?? DefaultExpiry;
            entry.RegisterPostEvictionCallback(static (evictedKey, _, _, state) =>
            {
                if (state is ConcurrentDictionary<string, byte> keys && evictedKey is string sk)
                    keys.TryRemove(sk, out _);
            }, _knownKeys);

            return await factory().ConfigureAwait(false);
        }).ConfigureAwait(false);

        _knownKeys.TryAdd(key, 0);

        if (created)
        {
            _metrics.RecordMiss();
            _logger.LogDebug("Cache miss for key {CacheKey}; value created", key);
        }
        else
        {
            _metrics.RecordHit();
            _logger.LogDebug("Cache hit for key {CacheKey}", key);
        }

        // GetOrCreateAsync may return null for reference types when factory returns null.
        return result!;
    }

    public Task RemoveAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _cache.Remove(key);
        _knownKeys.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        foreach (var key in _knownKeys.Keys)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            _cache.Remove(key);
            _knownKeys.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }
}

