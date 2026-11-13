using System.Collections.Concurrent;
using System.Text.Json;
using KasseAPI_Final.Services.Metrics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace KasseAPI_Final.Services.Caching;

/// <summary>
/// Redis-backed <see cref="ICacheService"/> via <see cref="IDistributedCache"/> (JSON payloads).
/// On Redis failures, transparently falls back to process-local <see cref="IMemoryCache"/>.
/// Hit/miss and get/set/remove are logged at Debug; Redis failures at Error.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _memoryFallback;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly ICacheMetricsService _metrics;
    private readonly ConcurrentDictionary<string, byte> _knownKeys = new(StringComparer.Ordinal);
    private int _fallbackLogged;
    /// <summary>1 = Redis reachable; 0 = last Redis ops failed (memory fallback active).</summary>
    private int _redisAvailable = 1;

    public RedisCacheService(
        IDistributedCache distributedCache,
        IMemoryCache memoryFallback,
        ILogger<RedisCacheService> logger,
        ICacheMetricsService metrics)
    {
        ArgumentNullException.ThrowIfNull(distributedCache);
        ArgumentNullException.ThrowIfNull(memoryFallback);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(metrics);

        _distributedCache = distributedCache;
        _memoryFallback = memoryFallback;
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// Whether the last Redis (<see cref="IDistributedCache"/>) operations succeeded.
    /// Becomes <c>false</c> after a transient Redis failure (memory fallback in use);
    /// returns to <c>true</c> after a successful Redis round-trip. Transparent to
    /// <see cref="ICacheService"/> callers — they always get memory fallback on failure.
    /// </summary>
    public bool IsRedisAvailable => Volatile.Read(ref _redisAvailable) == 1;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var cached = await _distributedCache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);
            MarkRedisAvailable();
            if (cached is null)
            {
                if (TryGetFallback(key, out T? fallbackHit))
                {
                    _metrics.RecordHit();
                    LogOp(key, "get", hit: true, viaFallback: true);
                    return fallbackHit;
                }

                _metrics.RecordMiss();
                LogOp(key, "get", hit: false);
                return default;
            }

            try
            {
                var deserialized = JsonSerializer.Deserialize<T>(cached, JsonOptions);
                _metrics.RecordHit();
                LogOp(key, "get", hit: true);
                _knownKeys.TryAdd(key, 0);
                return deserialized;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid cache payload for key {CacheKey}; removing", key);
                await _distributedCache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
                _knownKeys.TryRemove(key, out _);
                _metrics.RecordMiss();
                LogOp(key, "get", hit: false);
                return default;
            }
        }
        catch (Exception ex) when (IsTransientCacheFailure(ex))
        {
            ActivateFallback(ex, key, "get");
            if (TryGetFallback(key, out T? fallbackHit))
            {
                _metrics.RecordHit();
                LogOp(key, "get", hit: true, viaFallback: true);
                return fallbackHit;
            }

            _metrics.RecordMiss();
            LogOp(key, "get", hit: false, viaFallback: true);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var ttl = expiry ?? DefaultExpiry;

        try
        {
            var serialized = JsonSerializer.Serialize(value, JsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
            };

            await _distributedCache.SetStringAsync(key, serialized, options, cancellationToken)
                .ConfigureAwait(false);
            MarkRedisAvailable();
            _knownKeys[key] = 0;
            _metrics.RecordSize(serialized.Length);
            // Keep memory mirror for seamless fallback after Redis blips.
            SetFallback(key, value, ttl);
            LogOp(key, "set", hit: null);
        }
        catch (Exception ex) when (IsTransientCacheFailure(ex))
        {
            ActivateFallback(ex, key, "set");
            SetFallback(key, value, ttl);
            LogOp(key, "set", hit: null, viaFallback: true);
        }
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        try
        {
            var cached = await _distributedCache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);
            MarkRedisAvailable();
            if (cached is not null)
            {
                try
                {
                    var deserialized = JsonSerializer.Deserialize<T>(cached, JsonOptions);
                    if (deserialized is not null || typeof(T).IsValueType)
                    {
                        _metrics.RecordHit();
                        LogOp(key, "get", hit: true);
                        _knownKeys.TryAdd(key, 0);
                        return deserialized!;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Invalid cache payload for key {CacheKey}; removing", key);
                    await _distributedCache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
                    _knownKeys.TryRemove(key, out _);
                }
            }
        }
        catch (Exception ex) when (IsTransientCacheFailure(ex))
        {
            ActivateFallback(ex, key, "get");
            if (TryGetFallback(key, out T? fallbackHit) && (fallbackHit is not null || typeof(T).IsValueType))
            {
                _metrics.RecordHit();
                LogOp(key, "get", hit: true, viaFallback: true);
                return fallbackHit!;
            }
        }

        if (TryGetFallback(key, out T? memoryHit) && (memoryHit is not null || typeof(T).IsValueType)
            && _memoryFallback.TryGetValue(key, out _))
        {
            _metrics.RecordHit();
            LogOp(key, "get", hit: true, viaFallback: true);
            return memoryHit!;
        }

        var result = await factory(cancellationToken).ConfigureAwait(false);
        await SetAsync(key, result, expiry, cancellationToken).ConfigureAwait(false);
        _metrics.RecordMiss();
        LogOp(key, "get", hit: false);
        return result;
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _memoryFallback.Remove(key);
        _knownKeys.TryRemove(key, out _);

        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            MarkRedisAvailable();
            LogOp(key, "remove", hit: null);
        }
        catch (Exception ex) when (IsTransientCacheFailure(ex))
        {
            ActivateFallback(ex, key, "remove");
            LogOp(key, "remove", hit: null, viaFallback: true);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var matching = _knownKeys.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();

        foreach (var key in matching)
            await RemoveAsync(key, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Cache op=remove-by-prefix key={CachePrefix} count={Count}", prefix, matching.Length);
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var keys = _knownKeys.Keys.ToArray();
        foreach (var key in keys)
            await RemoveAsync(key, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Cache op=clear-all count={Count}", keys.Length);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var cached = await _distributedCache.GetAsync(key, cancellationToken).ConfigureAwait(false);
            MarkRedisAvailable();
            if (cached is { Length: > 0 })
                return true;
        }
        catch (Exception ex) when (IsTransientCacheFailure(ex))
        {
            ActivateFallback(ex, key, "exists");
        }

        return _memoryFallback.TryGetValue(key, out _);
    }

    private bool TryGetFallback<T>(string key, out T? value) =>
        _memoryFallback.TryGetValue(key, out value);

    private void SetFallback<T>(string key, T value, TimeSpan ttl)
    {
        _memoryFallback.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
        });
        _knownKeys[key] = 0;
    }

    private void MarkRedisAvailable()
    {
        // Only log recovery when transitioning from unavailable → available.
        if (Interlocked.Exchange(ref _redisAvailable, 1) == 0)
        {
            Interlocked.Exchange(ref _fallbackLogged, 0);
            _logger.LogInformation("Redis is available again, switching back from MemoryCache");
        }
    }

    private void ActivateFallback(Exception ex, string key, string operation)
    {
        Volatile.Write(ref _redisAvailable, 0);

        if (Interlocked.Exchange(ref _fallbackLogged, 1) == 0)
        {
            _logger.LogError(
                ex,
                "Redis operation failed, falling back to MemoryCache. Key: {Key}, Operation: {Operation}",
                key,
                operation);
        }
        else
        {
            _logger.LogError(
                ex,
                "Redis operation failed, using MemoryCache fallback. Key: {Key}, Operation: {Operation}",
                key,
                operation);
        }
    }

    private void LogOp(string key, string operation, bool? hit, bool viaFallback = false)
    {
        if (hit is null)
        {
            _logger.LogDebug(
                "Cache op={CacheOperation} key={CacheKey} fallback={CacheFallback}",
                operation,
                key,
                viaFallback);
            return;
        }

        // Hits and misses are both Debug — enable KasseAPI_Final.Services.Caching=Debug (or Information
        // will not show these) when troubleshooting in Development.
        _logger.LogDebug(
            "Cache op={CacheOperation} key={CacheKey} hit={CacheHit} fallback={CacheFallback}",
            operation,
            key,
            hit.Value,
            viaFallback);
    }

    private static bool IsTransientCacheFailure(Exception ex)
    {
        if (ex is TimeoutException or IOException or ObjectDisposedException)
            return true;

        var name = ex.GetType().FullName ?? ex.GetType().Name;
        return name.Contains("Redis", StringComparison.OrdinalIgnoreCase);
    }
}
