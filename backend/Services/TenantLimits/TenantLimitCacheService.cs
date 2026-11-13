using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Caching;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Limits;

/// <summary>
/// <see cref="ICacheService"/>-backed tenant limits cache (Cache-Aside).
/// Key pattern: <see cref="CacheKeys.TenantLimits"/>.
/// </summary>
public sealed class TenantLimitCacheService : ITenantLimitCacheService
{
    private readonly ICacheService _cache;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<TenantLimitCacheService> _logger;

    public TenantLimitCacheService(
        ICacheService cache,
        IOptions<CacheSettings> cacheSettings,
        ILogger<TenantLimitCacheService> logger)
    {
        _cache = cache;
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
    }

    public static string BuildKey(Guid tenantId) => CacheKeys.Format(CacheKeys.TenantLimits, tenantId);

    public Task<TenantLimits> GetOrCreateAsync(
        Guid tenantId,
        Func<CancellationToken, Task<TenantLimits>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return _cache.GetOrCreateAsync(
            BuildKey(tenantId),
            factory,
            _cacheSettings.TenantLimitsCacheTtl,
            cancellationToken);
    }

    public async Task InvalidateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(tenantId);
        await CacheInvalidationHelper.InvalidateTenantCacheAsync(
                _cache,
                tenantId,
                cancellationToken,
                CacheKeys.TenantLimits)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "Tenant limits cache invalidated for tenant {TenantId} (key={CacheKey})",
            tenantId,
            key);
    }
}
