namespace KasseAPI_Final.Services.Caching;

/// <summary>Clears domain cache entries by tenant, prefix, or all tracked keys.</summary>
public sealed class CacheManagementService : ICacheManagementService
{
    private readonly ICacheService _cache;
    private readonly ILogger<CacheManagementService> _logger;

    public CacheManagementService(ICacheService cache, ILogger<CacheManagementService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<ClearCacheResult> ClearAsync(
        ClearCacheRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ClearAll)
        {
            await _cache.ClearAllAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Application cache cleared (clearAll=true)");
            return new ClearCacheResult
            {
                Success = true,
                Mode = "all",
                Detail = "All tracked cache keys removed.",
            };
        }

        if (!string.IsNullOrWhiteSpace(request.Prefix))
        {
            var prefix = request.Prefix.Trim();
            await _cache.RemoveByPrefixAsync(prefix, cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Cache cleared by prefix {CachePrefix}", prefix);
            return new ClearCacheResult
            {
                Success = true,
                Mode = "prefix",
                Detail = $"Removed keys with prefix '{prefix}'.",
            };
        }

        if (request.TenantId is { } tenantId)
        {
            await CacheInvalidationHelper.InvalidateAllTenantDomainCacheAsync(_cache, tenantId, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogWarning("Tenant cache cleared for {TenantId}", tenantId);
            return new ClearCacheResult
            {
                Success = true,
                Mode = "tenant",
                Detail = $"Removed license status and product list cache for tenant {tenantId}.",
            };
        }

        return new ClearCacheResult
        {
            Success = false,
            Mode = "none",
            Detail = "Specify tenantId, prefix, or clearAll=true.",
        };
    }
}
