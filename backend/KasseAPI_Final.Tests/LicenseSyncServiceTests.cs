using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseSyncServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicenseSync_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    [Fact]
    public async Task SyncTenantLicenseExpiryAsync_CopiesActiveIssuedExpiryToTenant()
    {
        await using var db = CreateDb();
        var tenantId = DemoTenantIds.Prod;
        var expiry = DateTime.UtcNow.AddDays(30);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Bar",
            Slug = "prod",
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseKey = "REGK-AAAAA-BBBBB-CCCCC",
            LicenseValidUntilUtc = DateTime.UtcNow.AddDays(5),
            CreatedAt = DateTime.UtcNow,
        });
        db.IssuedLicenses.Add(new IssuedLicense
        {
            Id = Guid.NewGuid(),
            LicenseKey = "REGK-AAAAA-BBBBB-CCCCC",
            CustomerName = "Test Bar",
            ExpiryAtUtc = expiry,
            SignedJwt = "jwt",
            IssuedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = new LicenseSyncService(db, Mock.Of<ILogger<LicenseSyncService>>());
        await service.SyncTenantLicenseExpiryAsync(tenantId);

        var reloaded = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(expiry, reloaded.LicenseValidUntilUtc);
    }

    [Fact]
    public async Task SyncTenantLicenseExpiryAsync_TrialWithoutKey_DoesNotOverwrite()
    {
        await using var db = CreateDb();
        var tenantId = DemoTenantIds.Prod;
        var trialUntil = DateTime.UtcNow.AddDays(30);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Bar",
            Slug = "prod",
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseKey = null,
            LicenseValidUntilUtc = trialUntil,
            CreatedAt = DateTime.UtcNow,
        });
        db.IssuedLicenses.Add(new IssuedLicense
        {
            Id = Guid.NewGuid(),
            LicenseKey = "REGK-OTHER1-OTHER2-OTHER3",
            CustomerName = "Test Bar",
            ExpiryAtUtc = DateTime.UtcNow.AddDays(365),
            SignedJwt = "jwt",
            IssuedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = new LicenseSyncService(db, Mock.Of<ILogger<LicenseSyncService>>());
        await service.SyncTenantLicenseExpiryAsync(tenantId);

        var reloaded = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(trialUntil, reloaded.LicenseValidUntilUtc);
    }

    [Fact]
    public async Task SyncTenantLicenseExpiryAsync_TestOverrideKey_DoesNotOverwrite()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var testUntil = DateTime.UtcNow.AddDays(1);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "QA",
            Slug = "dev",
            Status = TenantStatuses.Active,
            LicenseKey = "TEST-abc123",
            LicenseValidUntilUtc = testUntil,
            CreatedAt = DateTime.UtcNow,
        });
        db.IssuedLicenses.Add(new IssuedLicense
        {
            Id = Guid.NewGuid(),
            LicenseKey = "REGK-AAAAA-BBBBB-CCCCC",
            CustomerName = "QA",
            ExpiryAtUtc = DateTime.UtcNow.AddDays(999),
            SignedJwt = "jwt",
            IssuedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = new LicenseSyncService(db, Mock.Of<ILogger<LicenseSyncService>>());
        await service.SyncTenantLicenseExpiryAsync(tenantId);

        var reloaded = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(testUntil, reloaded.LicenseValidUntilUtc);
    }
}
