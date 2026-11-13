using KasseAPI_Final.Services.Caching;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KasseAPI_Final.HealthChecks;

/// <summary>
/// Ready/deps probe for domain cache (Redis when enabled, otherwise in-process memory).
/// Pings via <see cref="ICacheService"/> with a short timeout; failure →
/// <see cref="HealthStatus.Degraded"/> (not Unhealthy) so memory fallback does not fail readiness.
/// When the registered cache is <see cref="RedisCacheService"/>, uses
/// <see cref="RedisCacheService.IsRedisAvailable"/> so a successful memory-fallback ping still
/// reports Degraded Redis posture.
/// </summary>
public sealed class RedisCacheHealthCheck : IHealthCheck
{
    public const string Name = "cache";

    /// <summary>Probe timeout for set+get round-trip (avoids hanging ready).</summary>
    public const int TimeoutMilliseconds = 1000;

    private readonly ICacheService _cache;
    private readonly ILogger<RedisCacheHealthCheck> _logger;

    public RedisCacheHealthCheck(ICacheService cache, ILogger<RedisCacheHealthCheck> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeoutMilliseconds);

        try
        {
            // Write then GetAsync so Redis (or memory) connectivity is verified within the timeout.
            await _cache.SetAsync(
                    CacheKeys.HealthPing,
                    "ok",
                    TimeSpan.FromSeconds(30),
                    timeoutCts.Token)
                .ConfigureAwait(false);

            var value = await _cache.GetAsync<string>(CacheKeys.HealthPing, timeoutCts.Token)
                .ConfigureAwait(false);

            // Prefer RedisCacheService.IsRedisAvailable so memory fallback does not mask Redis outage.
            if (_cache is RedisCacheService redisCache && !redisCache.IsRedisAvailable)
            {
                _logger.LogWarning(
                    "Cache health ping used memory fallback; RedisCacheService.IsRedisAvailable=false");
                return HealthCheckResult.Degraded(
                    "Redis unavailable; domain cache serving from in-process IMemoryCache fallback.",
                    data: ProbeData("Degraded", redisAvailable: false));
            }

            if (!string.Equals(value, "ok", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Cache health ping key {CacheKey} missing or unexpected after set (Redis may be unavailable)",
                    CacheKeys.HealthPing);
                return HealthCheckResult.Degraded(
                    "Cache ping write succeeded but GetAsync did not return the probe value.",
                    data: ProbeData("Degraded", redisAvailable: _cache is not RedisCacheService));
            }

            return HealthCheckResult.Healthy(
                _cache is RedisCacheService
                    ? "Redis cache reachable."
                    : "Cache reachable (in-process memory).",
                data: ProbeData("Healthy", redisAvailable: true));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Cache health check timed out after {TimeoutMs}ms (Redis connection may be unavailable)",
                TimeoutMilliseconds);
            return HealthCheckResult.Degraded(
                $"Cache ping timed out after {TimeoutMilliseconds}ms.",
                data: ProbeData("Degraded", redisAvailable: false));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Cache/Redis health check failed for key {CacheKey}",
                CacheKeys.HealthPing);
            return HealthCheckResult.Degraded(
                "Cache unavailable: " + ex.Message,
                exception: ex,
                data: ProbeData("Degraded", redisAvailable: false));
        }
    }

    private static Dictionary<string, object> ProbeData(string redisStatus, bool redisAvailable) =>
        new(StringComparer.Ordinal)
        {
            ["cacheKey"] = CacheKeys.HealthPing,
            ["redisStatus"] = redisStatus,
            ["isRedisAvailable"] = redisAvailable,
        };
}
