using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Services.Metrics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseCacheServiceTests
{
    [Fact]
    public async Task InvalidateForTenantAsync_RemovesStatusAndTenantKeys()
    {
        var memory = CreateMemory();
        var statusCache = new LicenseStatusCache(
            memory,
            Options.Create(new CacheSettings()),
            NullLogger<LicenseStatusCache>.Instance);
        var sut = new LicenseCacheService(
            memory,
            statusCache,
            LicenseKeyValidator.Instance,
            NullLogger<LicenseCacheService>.Instance);
        var tenantId = Guid.NewGuid();

        await statusCache.GetOrCreateAsync(
            tenantId,
            _ => Task.FromResult(new TenantLicenseStatus { Status = "valid", IsValid = true }));
        await memory.SetAsync(CacheKeys.Format(CacheKeys.LicenseTenant, tenantId), "overlay");
        await memory.SetAsync(CacheKeys.LicenseAdminList, "list");
        await memory.SetAsync(CacheKeys.LicenseBillingSales, "sales");

        await sut.InvalidateForTenantAsync(tenantId);

        Assert.False(await memory.ExistsAsync(LicenseStatusCache.BuildKey(tenantId)));
        Assert.False(await memory.ExistsAsync(CacheKeys.Format(CacheKeys.LicenseTenant, tenantId)));
        Assert.False(await memory.ExistsAsync(CacheKeys.LicenseAdminList));
        Assert.False(await memory.ExistsAsync(CacheKeys.LicenseBillingSales));
    }

    [Fact]
    public async Task InvalidateAllAsync_RemovesPerKeyLookup()
    {
        var memory = CreateMemory();
        var statusCache = new LicenseStatusCache(
            memory,
            Options.Create(new CacheSettings()),
            NullLogger<LicenseStatusCache>.Instance);
        var sut = new LicenseCacheService(
            memory,
            statusCache,
            LicenseKeyValidator.Instance,
            NullLogger<LicenseCacheService>.Instance);
        const string key = "REGK-20990101-cafe-A7F3K2D9";
        var lookup = CacheKeys.Format(CacheKeys.LicenseKeyLookup, key);
        await memory.SetAsync(lookup, "cached");

        await sut.InvalidateAllAsync(key);

        Assert.False(await memory.ExistsAsync(lookup));
    }

    private static MemoryCacheService CreateMemory() =>
        new(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<MemoryCacheService>.Instance,
            new CacheMetricsService());
}
