using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DigitalServices;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantServiceStatusServiceTests
{
    [Fact]
    public async Task ListTenantStatusesAsync_returns_defaults_when_no_rows()
    {
        var (sut, db) = CreateSut(nameof(ListTenantStatusesAsync_returns_defaults_when_no_rows));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe Default",
            Slug = "cafe-default",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var rows = await sut.ListTenantStatusesAsync();
        var row = Assert.Single(rows);
        Assert.Equal(tenantId, row.TenantId);
        Assert.True(row.Website.IsActive);
        Assert.True(row.Website.IsEnabled);
        Assert.True(row.Website.IsAvailable);
        Assert.Null(row.Website.CustomPrice);
        Assert.True(row.Website.Price > 0);
        Assert.True(row.App.IsActive);
    }

    [Fact]
    public async Task SetActiveAsync_deactivates_and_reactivates()
    {
        var (sut, db) = CreateSut(nameof(SetActiveAsync_deactivates_and_reactivates));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe Toggle",
            Slug = "cafe-toggle",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var off = await sut.SetActiveAsync(tenantId, "website", false, "actor-1", "maintenance");
        Assert.True(off.Succeeded);
        Assert.False(off.Tenant!.Website.IsActive);
        Assert.Equal("maintenance", off.Tenant.Website.DeactivationReason);

        var on = await sut.SetActiveAsync(tenantId, "website", true, "actor-1", null);
        Assert.True(on.Succeeded);
        Assert.True(on.Tenant!.Website.IsActive);
        Assert.Null(on.Tenant.Website.DeactivationReason);
    }

    [Fact]
    public async Task SetCustomPriceAsync_overrides_and_clears()
    {
        var (sut, db) = CreateSut(nameof(SetCustomPriceAsync_overrides_and_clears));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe Price",
            Slug = "cafe-price",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var set = await sut.SetCustomPriceAsync(tenantId, "app", 42m, "actor-2");
        Assert.True(set.Succeeded);
        Assert.Equal(42m, set.Tenant!.App.CustomPrice);
        Assert.Equal(42m, set.Tenant.App.Price);

        var clear = await sut.SetCustomPriceAsync(tenantId, "app", null, "actor-2");
        Assert.True(clear.Succeeded);
        Assert.Null(clear.Tenant!.App.CustomPrice);
        Assert.Equal(clear.Tenant.App.ListPrice, clear.Tenant.App.Price);
    }

    [Fact]
    public async Task SetEnabledAsync_toggles_mandant_preference()
    {
        var (sut, db) = CreateSut(nameof(SetEnabledAsync_toggles_mandant_preference));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe Enable",
            Slug = "cafe-enable",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var off = await sut.SetEnabledAsync(tenantId, "app", false, "actor-3");
        Assert.True(off.Succeeded);
        Assert.False(off.Tenant!.App.IsEnabled);
        Assert.False(off.Tenant.App.IsAvailable);

        var on = await sut.SetEnabledAsync(tenantId, "app", true, "actor-3");
        Assert.True(on.Succeeded);
        Assert.True(on.Tenant!.App.IsEnabled);
        Assert.True(on.Tenant.App.IsAvailable);
    }

    [Fact]
    public async Task IsServiceAvailableAsync_false_when_deactivated()
    {
        var (sut, db) = CreateSut(nameof(IsServiceAvailableAsync_false_when_deactivated));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe Gate",
            Slug = "cafe-gate",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        Assert.True(await sut.IsServiceAvailableAsync(tenantId, "website"));
        await sut.SetActiveAsync(tenantId, "website", false, "actor-4", "pause");
        Assert.False(await sut.IsServiceAvailableAsync(tenantId, "website"));
    }

    [Fact]
    public async Task GetForTenantAsync_returns_null_when_missing()
    {
        var (sut, _) = CreateSut(nameof(GetForTenantAsync_returns_null_when_missing));
        Assert.Null(await sut.GetForTenantAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetForTenantAsync_returns_defaults_for_existing_tenant()
    {
        var (sut, db) = CreateSut(nameof(GetForTenantAsync_returns_defaults_for_existing_tenant));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe Get",
            Slug = "cafe-get",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var row = await sut.GetForTenantAsync(tenantId);
        Assert.NotNull(row);
        Assert.Equal(tenantId, row!.TenantId);
        Assert.True(row.Website.IsAvailable);
        Assert.True(row.App.IsAvailable);
    }

    [Fact]
    public async Task MarkRequestPendingAsync_sets_pending_status()
    {
        var (sut, db) = CreateSut(nameof(MarkRequestPendingAsync_sets_pending_status));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Pending Cafe",
            Slug = "pending-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await sut.MarkRequestPendingAsync(tenantId, TenantServiceTypes.Website);

        var row = await db.TenantServiceStatuses.IgnoreQueryFilters()
            .SingleAsync(s => s.TenantId == tenantId && s.ServiceType == TenantServiceTypes.Website);
        Assert.Equal(TenantDigitalServiceStatuses.Pending, row.Status);
        Assert.NotNull(row.RequestedAt);
        Assert.True(row.HasRequest);
    }

    [Fact]
    public async Task MarkCreated_then_Published_updates_lifecycle()
    {
        var (sut, db) = CreateSut(nameof(MarkCreated_then_Published_updates_lifecycle));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Publish Cafe",
            Slug = "publish-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await sut.MarkCreatedAsync(tenantId, TenantServiceTypes.Website, "/sites/x/", "modern");
        await sut.MarkPublishedAsync(tenantId, TenantServiceTypes.Website, "/sites/x/");

        var row = await db.TenantServiceStatuses.IgnoreQueryFilters()
            .SingleAsync(s => s.TenantId == tenantId && s.ServiceType == TenantServiceTypes.Website);
        Assert.Equal(TenantDigitalServiceStatuses.Published, row.Status);
        Assert.Equal("/sites/x/", row.Url);
        Assert.Equal("modern", row.TemplateId);
        Assert.NotNull(row.ArtifactCreatedAt);
        Assert.NotNull(row.PublishedAt);
    }

    private static (TenantServiceStatusService Sut, AppDbContext Db) CreateSut(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        var sut = new TenantServiceStatusService(
            db,
            new DigitalServicePricingService(),
            TimeProvider.System,
            NullLogger<TenantServiceStatusService>.Instance);
        return (sut, db);
    }
}
