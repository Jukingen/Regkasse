using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Caching;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Billing;

/// <summary>
/// <see cref="ICacheService"/>-backed license status cache (Cache-Aside).
/// Key pattern: <see cref="CacheKeys.LicenseStatus"/>.
/// </summary>
public sealed class LicenseStatusCache : ILicenseStatusCache
{
    private readonly ICacheService _cache;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<LicenseStatusCache> _logger;

    public LicenseStatusCache(
        ICacheService cache,
        IOptions<CacheSettings> cacheSettings,
        ILogger<LicenseStatusCache> logger)
    {
        _cache = cache;
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
    }

    /// <summary>Builds <c>license_status_{tenantId}</c>.</summary>
    public static string BuildKey(Guid tenantId) => CacheKeys.Format(CacheKeys.LicenseStatus, tenantId);

    public Task<TenantLicenseStatus> GetOrCreateAsync(
        Guid tenantId,
        Func<CancellationToken, Task<TenantLicenseStatus>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return _cache.GetOrCreateAsync(
            BuildKey(tenantId),
            factory,
            _cacheSettings.LicenseCacheTtl,
            cancellationToken);
    }

    public async Task InvalidateLicenseCacheAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(tenantId);
        await CacheInvalidationHelper.InvalidateTenantCacheAsync(
                _cache,
                tenantId,
                cancellationToken,
                CacheKeys.LicenseStatus)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "License cache invalidated for tenant {TenantId} (key={CacheKey})",
            tenantId,
            key);
    }
}
