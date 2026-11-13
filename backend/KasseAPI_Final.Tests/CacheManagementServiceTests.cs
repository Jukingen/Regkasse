using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Metrics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CacheManagementServiceTests
{
    [Fact]
    public async Task ClearAsync_ByTenant_RemovesLicenseAndProductPrefixes()
    {
        var cache = CreateCache();
        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        await cache.SetAsync(CacheKeys.Format(CacheKeys.LicenseStatus, tenantId), "stale");
        await cache.SetAsync(CacheKeys.Format(CacheKeys.ProductList, tenantId), "list");
        await cache.SetAsync(CacheKeys.Format(CacheKeys.ProductListByCategory, tenantId, categoryId), "filtered");
        await cache.SetAsync("other", "keep");

        var sut = new CacheManagementService(cache, NullLogger<CacheManagementService>.Instance);
        var result = await sut.ClearAsync(new ClearCacheRequest { TenantId = tenantId });

        Assert.True(result.Success);
        Assert.Equal("tenant", result.Mode);
        Assert.False(await cache.ExistsAsync(CacheKeys.Format(CacheKeys.LicenseStatus, tenantId)));
        Assert.False(await cache.ExistsAsync(CacheKeys.Format(CacheKeys.ProductList, tenantId)));
        Assert.False(await cache.ExistsAsync(CacheKeys.Format(CacheKeys.ProductListByCategory, tenantId, categoryId)));
        Assert.True(await cache.ExistsAsync("other"));
    }

    [Fact]
    public async Task ClearAsync_ClearAll_RemovesTrackedKeys()
    {
        var cache = CreateCache();
        await cache.SetAsync("a", 1);
        await cache.SetAsync("b", 2);

        var sut = new CacheManagementService(cache, NullLogger<CacheManagementService>.Instance);
        var result = await sut.ClearAsync(new ClearCacheRequest { ClearAll = true });

        Assert.True(result.Success);
        Assert.Equal("all", result.Mode);
        Assert.False(await cache.ExistsAsync("a"));
        Assert.False(await cache.ExistsAsync("b"));
    }

    private static MemoryCacheService CreateCache() =>
        new(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<MemoryCacheService>.Instance,
            new CacheMetricsService());
}
