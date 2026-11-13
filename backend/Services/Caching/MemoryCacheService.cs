using System.Collections.Concurrent;
using KasseAPI_Final.Services.Metrics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Services.Caching;

/// <summary>
/// <see cref="IMemoryCache"/>-backed <see cref="ICacheService"/> for Development and tests.
/// Tracks keys so prefix / clear-all works without Redis.
/// </summary>
public sealed class MemoryCacheService : ICacheService
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly ICacheMetricsService _metrics;
    private readonly IHostEnvironment? _environment;
    private readonly ConcurrentDictionary<string, byte> _knownKeys = new(StringComparer.Ordinal);

    public MemoryCacheService(
        IMemoryCache cache,
        ILogger<MemoryCacheService> logger,
        ICacheMetricsService metrics,
        IHostEnvironment? environment = null)
    {
        _cache = cache;
        _logger = logger;
        _metrics = metrics;
        _environment = environment;
    }

    private LogLevel OpLogLevel =>
        _environment?.IsDevelopment() == true ? LogLevel.Information : LogLevel.Debug;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(key, out T? value))
        {
            _metrics.RecordHit();
            LogOp(key, "get", hit: true);
            return Task.FromResult(value);
        }

        _metrics.RecordMiss();
        LogOp(key, "get", hit: false);
        return Task.FromResult(default(T));
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? DefaultExpiry,
        };
        options.RegisterPostEvictionCallback(static (evictedKey, _, _, state) =>
        {
            if (state is ConcurrentDictionary<string, byte> keys && evictedKey is string sk)
                keys.TryRemove(sk, out _);
        }, _knownKeys);

        _cache.Set(key, value, options);
        _knownKeys[key] = 0;
        LogOp(key, "set", hit: null);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
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

            return await factory(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

        _knownKeys.TryAdd(key, 0);

        if (created)
        {
            _metrics.RecordMiss();
            LogOp(key, "get", hit: false);
        }
        else
        {
            _metrics.RecordHit();
            LogOp(key, "get", hit: true);
        }

        return result!;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        _cache.Remove(key);
        _knownKeys.TryRemove(key, out _);
        LogOp(key, "remove", hit: null);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var key in _knownKeys.Keys)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            _cache.Remove(key);
            _knownKeys.TryRemove(key, out _);
        }

        _logger.Log(OpLogLevel, "Cache op=remove-by-prefix key={CachePrefix}", prefix);
        return Task.CompletedTask;
    }

    public Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var key in _knownKeys.Keys.ToArray())
        {
            _cache.Remove(key);
            _knownKeys.TryRemove(key, out _);
        }

        _logger.Log(OpLogLevel, "Cache op=clear-all");
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }

    private void LogOp(string key, string operation, bool? hit)
    {
        if (hit is null)
        {
            _logger.Log(OpLogLevel, "Cache op={CacheOperation} key={CacheKey}", operation, key);
            return;
        }

        _logger.Log(
            OpLogLevel,
            "Cache op={CacheOperation} key={CacheKey} hit={CacheHit}",
            operation,
            key,
            hit.Value);
    }
}
