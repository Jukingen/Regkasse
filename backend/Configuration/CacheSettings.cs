namespace KasseAPI_Final.Configuration;

/// <summary>
/// Domain cache TTLs for <see cref="Services.Caching.ICacheService"/> consumers.
/// Bound from <c>CacheSettings</c> in appsettings.
/// </summary>
public sealed class CacheSettings
{
    public const string SectionName = "CacheSettings";

    /// <summary>TTL for tenant license status (<c>license_status_{tenantId}</c>).</summary>
    public int LicenseCacheMinutes { get; set; } = 5;

    /// <summary>TTL for tenant product list cache (<c>product_list_{tenantId}</c>).</summary>
    public int ProductCacheMinutes { get; set; } = 15;

    /// <summary>TTL for effective user permission snapshots (<c>user_permissions_{userId}</c>).</summary>
    public int PermissionCacheMinutes { get; set; } = 30;

    /// <summary>TTL for tenant settings snapshots (<c>tenant_settings_{tenantId}</c>).</summary>
    public int TenantSettingsCacheMinutes { get; set; } = 60;

    /// <summary>TTL for per-tenant operational caps (<c>tenant_limits_{tenantId}</c>).</summary>
    public int TenantLimitsCacheMinutes { get; set; } = 5;

    /// <summary>
    /// TTL for optional TSE health snapshots via <see cref="Services.Caching.CacheKeys.TseHealth"/>.
    /// Process-wide TSE monitor uses in-memory snapshots today; this value is reserved for
    /// domain <c>ICacheService</c> consumers.
    /// </summary>
    public int TseHealthCacheSeconds { get; set; } = 30;

    public TimeSpan LicenseCacheTtl => TimeSpan.FromMinutes(Math.Max(1, LicenseCacheMinutes));

    public TimeSpan ProductCacheTtl => TimeSpan.FromMinutes(Math.Max(1, ProductCacheMinutes));

    public TimeSpan PermissionCacheTtl => TimeSpan.FromMinutes(Math.Max(1, PermissionCacheMinutes));

    public TimeSpan TenantSettingsCacheTtl => TimeSpan.FromMinutes(Math.Max(1, TenantSettingsCacheMinutes));

    public TimeSpan TenantLimitsCacheTtl => TimeSpan.FromMinutes(Math.Max(1, TenantLimitsCacheMinutes));

    public TimeSpan TseHealthCacheTtl => TimeSpan.FromSeconds(Math.Max(1, TseHealthCacheSeconds));
}
