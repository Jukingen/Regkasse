using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Metrics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests.Billing;

public sealed class LicenseStatusCacheTests
{
    [Fact]
    public async Task InvalidateLicenseCacheAsync_RemovesCachedStatus()
    {
        var cache = CreateLicenseStatusCache(out var memory);
        var tenantId = Guid.NewGuid();

        await cache.GetOrCreateAsync(
            tenantId,
            _ => Task.FromResult(new TenantLicenseStatus
            {
                Status = "valid",
                IsValid = true,
                LicenseKey = "REGK-20270101-cafe-TESTKEY1",
            }));

        Assert.True(await memory.ExistsAsync(LicenseStatusCache.BuildKey(tenantId)));

        await cache.InvalidateLicenseCacheAsync(tenantId);

        Assert.False(await memory.ExistsAsync(LicenseStatusCache.BuildKey(tenantId)));
    }

    [Fact]
    public async Task CreateLicenseSaleAsync_InvalidatesTenantStatusCacheAfterCommit()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var disposer = harness;

        var licenseStatusCache = CreateLicenseStatusCache(out var memory);

        var tenant = await harness.CreateTestTenantAsync();
        var actorUserId = await harness.CreateTestUserAsync();

        await licenseStatusCache.GetOrCreateAsync(
            tenant.Id,
            _ => Task.FromResult(new TenantLicenseStatus
            {
                Status = "none",
                IsValid = false,
            }));
        Assert.True(await memory.ExistsAsync(LicenseStatusCache.BuildKey(tenant.Id)));

        var billingService = harness.CreateBillingService(licenseStatusCache: licenseStatusCache);
        await billingService.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.TwelveMonths,
                PriceNet = 299.00m,
                VatRate = 20.00m,
                ApplyToTenant = true,
            },
            actorUserId);

        Assert.False(
            await memory.ExistsAsync(LicenseStatusCache.BuildKey(tenant.Id)),
            "Sale commit must drop license_status_{tenantId} so the next read is not stale.");
    }

    [Fact]
    public async Task GetCurrentStatusAsync_AfterInvalidate_ReloadsFromDatabase()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var disposer = harness;

        var licenseStatusCache = CreateLicenseStatusCache(out _);
        var tenant = await harness.CreateTestTenantAsync();
        var actorUserId = await harness.CreateTestUserAsync();
        var billingService = harness.CreateBillingService(licenseStatusCache: licenseStatusCache);
        var (db, _) = harness.CreateDbContextPair();

        var tenantLicenseService = new TenantLicenseService(
            db,
            billingService,
            new LicenseKeyGenerator(),
            BillingTestDoubles.CreateAuditService(db),
            licenseStatusCache,
            NullLogger<TenantLicenseService>.Instance);

        var before = await tenantLicenseService.GetCurrentStatusAsync(tenant.Id);
        Assert.Equal("none", before.Status);

        await billingService.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.TwelveMonths,
                PriceNet = 299.00m,
                VatRate = 20.00m,
                ApplyToTenant = true,
            },
            actorUserId);

        db.ChangeTracker.Clear();
        var after = await tenantLicenseService.GetCurrentStatusAsync(tenant.Id);
        Assert.Equal("valid", after.Status);
        Assert.True(after.IsValid);
        Assert.False(string.IsNullOrEmpty(after.LicenseKey));
    }

    private static LicenseStatusCache CreateLicenseStatusCache(out MemoryCacheService memory)
    {
        memory = new MemoryCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<MemoryCacheService>.Instance,
            new CacheMetricsService());
        return new LicenseStatusCache(memory, Options.Create(new CacheSettings()), NullLogger<LicenseStatusCache>.Instance);
    }
}
