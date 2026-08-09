namespace KasseAPI_Final.Services.Caching;

/// <summary>
/// Pure static helpers that wrap <see cref="ICacheService"/> removals so License / Product / User
/// code paths invalidate cache with the same key patterns. Contains no business logic.
/// </summary>
public static class CacheInvalidationHelper
{
    /// <summary>
    /// Invalidates one or more tenant-scoped cache keys.
    /// </summary>
    /// <param name="cache">Domain cache service.</param>
    /// <param name="tenantId">Tenant whose keys are invalidated.</param>
    /// <param name="keyPatterns">
    /// Format strings such as <see cref="CacheKeys.LicenseStatus"/> (<c>{0}</c> = tenant id),
    /// prefix constants ending with <c>_</c>, or already-built keys that include the tenant id.
    /// </param>
    public static Task InvalidateTenantCacheAsync(
        ICacheService cache,
        Guid tenantId,
        params string[] keyPatterns) =>
        InvalidateTenantCacheAsync(cache, tenantId, CancellationToken.None, keyPatterns);

    /// <inheritdoc cref="InvalidateTenantCacheAsync(ICacheService, Guid, string[])"/>
    public static async Task InvalidateTenantCacheAsync(
        ICacheService cache,
        Guid tenantId,
        CancellationToken cancellationToken,
        params string[] keyPatterns)
    {
        ArgumentNullException.ThrowIfNull(cache);
        if (keyPatterns is null || keyPatterns.Length == 0)
            return;

        foreach (var pattern in keyPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            var key = FormatTenantScopedKey(pattern.Trim(), tenantId);
            await cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Removes a single cache entry by exact key (thin wrapper around <see cref="ICacheService.RemoveAsync"/>).
    /// </summary>
    /// <param name="cache">Domain cache service.</param>
    /// <param name="key">Exact cache key to remove.</param>
    public static Task InvalidateSpecificCacheAsync(ICacheService cache, string key) =>
        InvalidateSpecificCacheAsync(cache, key, CancellationToken.None);

    /// <inheritdoc cref="InvalidateSpecificCacheAsync(ICacheService, string)"/>
    public static Task InvalidateSpecificCacheAsync(
        ICacheService cache,
        string key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return cache.RemoveAsync(key, cancellationToken);
    }

    /// <summary>
    /// Drops product list keys for a tenant (prefix clear so category-filtered keys are included)
    /// and optionally a single-product key.
    /// </summary>
    public static async Task InvalidateProductCacheAsync(
        ICacheService cache,
        Guid tenantId,
        Guid? productId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);

        // Category variants share the product_list_{tenantId} prefix — RemoveAsync alone is not enough.
        await cache.RemoveByPrefixAsync(CacheKeys.Format(CacheKeys.ProductList, tenantId), cancellationToken)
            .ConfigureAwait(false);

        if (productId is { } id)
        {
            await InvalidateSpecificCacheAsync(cache, CacheKeys.Format(CacheKeys.ProductDetail, id), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Drops license status cache for a tenant (<c>license_status_{tenantId}</c>).</summary>
    public static Task InvalidateLicenseCacheAsync(
        ICacheService cache,
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        InvalidateTenantCacheAsync(cache, tenantId, cancellationToken, CacheKeys.LicenseStatus);

    /// <summary>Drops effective permission snapshot for a user (<c>user_permissions_{userId}</c>).</summary>
    public static Task InvalidateUserPermissionsCacheAsync(
        ICacheService cache,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return InvalidateSpecificCacheAsync(cache, CacheKeys.Format(CacheKeys.UserPermissions, userId), cancellationToken);
    }

    /// <summary>
    /// Clears the common tenant domain keys (license status, product lists, tenant settings).
    /// Used by Super Admin cache clear-by-tenant.
    /// </summary>
    public static async Task InvalidateAllTenantDomainCacheAsync(
        ICacheService cache,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);

        await InvalidateTenantCacheAsync(
                cache,
                tenantId,
                cancellationToken,
                CacheKeys.LicenseStatus,
                CacheKeys.TenantSettings)
            .ConfigureAwait(false);

        await InvalidateProductCacheAsync(cache, tenantId, productId: null, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string FormatTenantScopedKey(string keyPattern, Guid tenantId)
    {
        if (keyPattern.Contains("{0}", StringComparison.Ordinal))
            return string.Format(keyPattern, tenantId);

        // Prefix constants such as CacheKeys.LicenseStatusPrefix ("license_status_")
        if (keyPattern.EndsWith('_'))
            return keyPattern + tenantId.ToString("D");

        return keyPattern;
    }
}
