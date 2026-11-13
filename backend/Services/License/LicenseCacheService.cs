using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.Caching;

namespace KasseAPI_Final.Services.License;

/// <summary>
/// Drops license lookup, tenant status, admin list, and billing-sales cache keys.
/// </summary>
public sealed class LicenseCacheService : ILicenseCacheService
{
    private readonly ICacheService _cache;
    private readonly ILicenseStatusCache _licenseStatusCache;
    private readonly ILicenseKeyValidator _validator;
    private readonly ILogger<LicenseCacheService> _logger;

    public LicenseCacheService(
        ICacheService cache,
        ILicenseStatusCache licenseStatusCache,
        ILicenseKeyValidator validator,
        ILogger<LicenseCacheService> logger)
    {
        _cache = cache;
        _licenseStatusCache = licenseStatusCache;
        _validator = validator;
        _logger = logger;
    }

    public async Task InvalidateAllAsync(string licenseKey, CancellationToken cancellationToken = default)
    {
        var parsed = _validator.Parse(licenseKey);
        var lookupKey = string.IsNullOrWhiteSpace(parsed.Normalized)
            ? (licenseKey ?? string.Empty).Trim()
            : parsed.Normalized;

        if (!string.IsNullOrEmpty(lookupKey))
        {
            await _cache
                .RemoveAsync(CacheKeys.Format(CacheKeys.LicenseKeyLookup, lookupKey), cancellationToken)
                .ConfigureAwait(false);
        }

        await RemoveAdminListAndSalesAsync(cancellationToken).ConfigureAwait(false);

        if (parsed.IsSystem || parsed.IsLegacyDisplay)
            await InvalidateForSystemAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("License cache invalidated for key lookup {KeyPrefix}", SafePrefix(lookupKey));
    }

    public async Task InvalidateForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return;

        await _licenseStatusCache
            .InvalidateLicenseCacheAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        await _cache
            .RemoveAsync(CacheKeys.Format(CacheKeys.LicenseTenant, tenantId), cancellationToken)
            .ConfigureAwait(false);

        await RemoveAdminListAndSalesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task InvalidateForSystemAsync(CancellationToken cancellationToken = default)
    {
        await RemoveAdminListAndSalesAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task RemoveAdminListAndSalesAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(
            _cache.RemoveAsync(CacheKeys.LicenseAdminList, cancellationToken),
            _cache.RemoveAsync(CacheKeys.LicenseBillingSales, cancellationToken));

    private static string SafePrefix(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "(empty)";
        return value.Length <= 12 ? value : value[..12] + "…";
    }
}
