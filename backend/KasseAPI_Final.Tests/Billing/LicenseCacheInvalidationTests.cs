using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Metrics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests.Billing;

/// <summary>
/// Regression: newly created license keys must be usable immediately without restart
/// (status cache invalidated after <see cref="BillingService.CreateLicenseSaleAsync"/>).
/// </summary>
public sealed class LicenseCacheInvalidationTests
{
    /// <summary>
    /// After create-license, <see cref="ICacheService.RemoveAsync"/> must run for
    /// <c>license_status_{tenantId}</c> (Moq verifies the exact key).
    /// </summary>
    [Fact]
    public async Task CreateLicense_ThenCacheIsInvalidated()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var disposer = harness;

        var tenant = await harness.CreateTestTenantAsync(slug: "cafe-moq");
        var actorUserId = await harness.CreateTestUserAsync();
        var expectedKey = CacheKeys.Format(CacheKeys.LicenseStatus, tenant.Id);

        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var licenseStatusCache = new LicenseStatusCache(
            cache.Object,
            Options.Create(new CacheSettings()),
            NullLogger<LicenseStatusCache>.Instance);

        var billingService = harness.CreateBillingService(licenseStatusCache: licenseStatusCache);

        await billingService.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.TwelveMonths,
                PriceNet = 299.00m,
                VatRate = 20.00m,
                ApplyToTenant = false,
            },
            actorUserId);

        cache.Verify(
            c => c.RemoveAsync(expectedKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateLicense_ThenValidateImmediately_ShouldSucceed()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var disposer = harness;

        var memory = CreateMemoryCacheService();
        var licenseStatusCache = new LicenseStatusCache(memory, Options.Create(new CacheSettings()), NullLogger<LicenseStatusCache>.Instance);

        var (db, _) = harness.CreateDbContextPair();
        var tenant = await harness.CreateTestTenantAsync(slug: "cafe");
        var actorUserId = await harness.CreateTestUserAsync();

        var billingService = harness.CreateBillingService(licenseStatusCache: licenseStatusCache);
        var tenantLicenseService = new TenantLicenseService(
            db,
            billingService,
            new LicenseKeyGenerator(),
            BillingTestDoubles.CreateAuditService(db),
            licenseStatusCache,
            NullLogger<TenantLicenseService>.Instance);

        // Warm Cache-Aside with a "no license" snapshot (simulates FA status check before sale).
        var beforeStatus = await tenantLicenseService.GetCurrentStatusAsync(tenant.Id);
        Assert.Equal("none", beforeStatus.Status);
        Assert.False(beforeStatus.IsValid);
        Assert.True(await memory.ExistsAsync(LicenseStatusCache.BuildKey(tenant.Id)));

        // Create a new license_sales row without applying it to the tenant yet
        // (Mandant will enter the key immediately — classic "License not found" race).
        var created = await billingService.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.TwelveMonths,
                PriceNet = 299.00m,
                VatRate = 20.00m,
                ApplyToTenant = false,
            },
            actorUserId);

        Assert.False(
            string.IsNullOrWhiteSpace(created.LicenseKey),
            "Created sale must include a license key.");
        Assert.False(
            await memory.ExistsAsync(LicenseStatusCache.BuildKey(tenant.Id)),
            "CreateLicenseSaleAsync must invalidate license_status_{tenantId} after commit.");

        // Immediately validate/activate the same key — no process restart, no manual cache clear.
        var activation = await tenantLicenseService.ActivateLicenseAsync(
            tenant.Id,
            created.LicenseKey,
            actorUserId);

        Assert.True(
            activation.Success,
            $"Expected activation success after create; got: {activation.Message}");
        Assert.DoesNotContain(
            "nicht gefunden",
            activation.Message ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(created.LicenseKey, activation.LicenseKey);
        Assert.Equal(created.ValidUntilUtc, activation.ValidUntilUtc);

        var afterStatus = await tenantLicenseService.GetCurrentStatusAsync(tenant.Id);
        Assert.True(afterStatus.IsValid);
        Assert.Equal("valid", afterStatus.Status);
        Assert.Equal(created.LicenseKey, afterStatus.LicenseKey);
        Assert.True(await tenantLicenseService.IsLicenseValidAsync(tenant.Id));
    }

    [Fact]
    public async Task CreateLicense_AppliedToTenant_ThenStatusReadImmediately_ShouldReflectNewLicense()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var disposer = harness;

        var memory = CreateMemoryCacheService();
        var licenseStatusCache = new LicenseStatusCache(memory, Options.Create(new CacheSettings()), NullLogger<LicenseStatusCache>.Instance);

        var (db, _) = harness.CreateDbContextPair();
        var tenant = await harness.CreateTestTenantAsync(slug: "bistro");
        var actorUserId = await harness.CreateTestUserAsync();

        var billingService = harness.CreateBillingService(licenseStatusCache: licenseStatusCache);
        var tenantLicenseService = new TenantLicenseService(
            db,
            billingService,
            new LicenseKeyGenerator(),
            BillingTestDoubles.CreateAuditService(db),
            licenseStatusCache,
            NullLogger<TenantLicenseService>.Instance);

        // Stale negative cache entry that would hide the new sale if invalidation regressed.
        await licenseStatusCache.GetOrCreateAsync(
            tenant.Id,
            _ => Task.FromResult(new TenantLicenseStatus
            {
                Status = "none",
                IsValid = false,
            }));
        Assert.True(await memory.ExistsAsync(LicenseStatusCache.BuildKey(tenant.Id)));

        var created = await billingService.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.TwelveMonths,
                PriceNet = 299.00m,
                VatRate = 20.00m,
                ApplyToTenant = true,
            },
            actorUserId);

        Assert.False(await memory.ExistsAsync(LicenseStatusCache.BuildKey(tenant.Id)));

        // Same process: status read must miss cache and reload from license_sales / tenant.
        var status = await tenantLicenseService.GetCurrentStatusAsync(tenant.Id);
        Assert.True(status.IsValid);
        Assert.Equal(created.LicenseKey, status.LicenseKey);
        Assert.Equal(created.ValidUntilUtc, status.ValidUntilUtc);

        var saleLookup = await billingService.GetSaleByLicenseKeyAsync(created.LicenseKey);
        Assert.NotNull(saleLookup);
        Assert.Equal(created.Id, saleLookup!.Id);
    }

    private static MemoryCacheService CreateMemoryCacheService() =>
        new(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<MemoryCacheService>.Instance,
            new CacheMetricsService());
}
