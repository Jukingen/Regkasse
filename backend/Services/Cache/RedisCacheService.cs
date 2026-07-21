using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Metrics;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace KasseAPI_Final.Services.Cache;

/// <summary>
/// Redis-backed <see cref="ICacheService"/> using JSON serialization.
/// Keys are prefixed with <see cref="RedisOptions.InstanceName"/>.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly ICacheMetricsService _metrics;
    private readonly string _keyPrefix;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options,
        ILogger<RedisCacheService> logger,
        ICacheMetricsService metrics)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(metrics);
        _db = redis.GetDatabase();
        _logger = logger;
        _metrics = metrics;

        var instance = options.Value.InstanceName?.Trim();
        _keyPrefix = string.IsNullOrEmpty(instance) ? string.Empty : instance.TrimEnd(':') + ":";
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        var redisKey = Prefixed(key);
        var cached = await _db.StringGetAsync(redisKey).ConfigureAwait(false);
        if (cached.HasValue)
        {
            try
            {
                var deserialized = JsonSerializer.Deserialize<T>((string)cached!, JsonOptions);
                if (deserialized is not null || typeof(T).IsValueType)
                {
                    _metrics.RecordHit();
                    _logger.LogDebug("Cache hit for key {CacheKey}", redisKey);
                    return deserialized!;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid cache payload for key {CacheKey}; removing", redisKey);
                await _db.KeyDeleteAsync(redisKey).ConfigureAwait(false);
            }
        }

        var result = await factory().ConfigureAwait(false);
        var serialized = JsonSerializer.Serialize(result, JsonOptions);
        await _db.StringSetAsync(redisKey, serialized, expiry ?? DefaultExpiry).ConfigureAwait(false);
        _metrics.RecordSize(serialized.Length);
        _metrics.RecordMiss();
        _logger.LogDebug("Cache miss for key {CacheKey}; value created", redisKey);
        return result;
    }

    public async Task RemoveAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var redisKey = Prefixed(key);
        await _db.KeyDeleteAsync(redisKey).ConfigureAwait(false);
        _logger.LogDebug("Cache removed for key {CacheKey}", redisKey);
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var endpoints = _db.Multiplexer.GetEndPoints();
        if (endpoints.Length == 0)
            return;

        var server = _db.Multiplexer.GetServer(endpoints[0]);
        var pattern = Prefixed(prefix) + "*";
        var keys = server.Keys(pattern: pattern).ToArray();

        if (keys.Length == 0)
            return;

        await _db.KeyDeleteAsync(keys).ConfigureAwait(false);
        _logger.LogDebug("Cache removed for prefix {CachePrefix}, {Count} keys", Prefixed(prefix), keys.Length);
    }

    public Task<bool> ExistsAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _db.KeyExistsAsync(Prefixed(key));
    }

    private string Prefixed(string key) => _keyPrefix + key;
}
