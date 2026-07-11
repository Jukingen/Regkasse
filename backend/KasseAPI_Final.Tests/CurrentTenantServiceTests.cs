using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CurrentTenantServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CurrentTenant_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ApplyCurrentTenantAsync_ResolvesSlugToTenantGuid()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        db.Tenants.Add(new Tenant { Id = tenantB, Name = "B", Slug = "companyb" });
        await db.SaveChangesAsync();

        var accessor = new CurrentTenantAccessor();
        var provider = new Mock<ITenantProvider>();
        provider.Setup(p => p.GetCurrentTenantId()).Returns("companyb");

        var service = new CurrentTenantService(
            provider.Object,
            accessor,
            db,
            NullLogger<CurrentTenantService>.Instance);

        await service.ApplyCurrentTenantAsync();

        Assert.Equal(tenantB, accessor.TenantId);
    }

    [Fact]
    public async Task ApplyCurrentTenantAsync_AdminSlug_MapsToDefaultTenant()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);

        var accessor = new CurrentTenantAccessor();
        var provider = new Mock<ITenantProvider>();
        provider.Setup(p => p.GetCurrentTenantId()).Returns("admin");

        var service = new CurrentTenantService(
            provider.Object,
            accessor,
            db,
            NullLogger<CurrentTenantService>.Instance);

        await service.ApplyCurrentTenantAsync();

        Assert.Equal(LegacyDefaultTenantIds.Primary, accessor.TenantId);
    }

    [Fact]
    public async Task ApplyCurrentTenantAsync_LegacyTestCafeAlias_ResolvesDevTenant()
    {
        await using var db = CreateContext();
        db.Tenants.Add(new Tenant
        {
            Id = DemoTenantIds.Dev,
            Name = "Development",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var accessor = new CurrentTenantAccessor();
        var provider = new Mock<ITenantProvider>();
        provider.Setup(p => p.GetCurrentTenantId()).Returns("test_cafe");

        var service = new CurrentTenantService(
            provider.Object,
            accessor,
            db,
            NullLogger<CurrentTenantService>.Instance);

        await service.ApplyCurrentTenantAsync();

        Assert.Equal(DemoTenantIds.Dev, accessor.TenantId);
    }

    [Fact]
    public async Task ApplyCurrentTenantAsync_UnknownSlug_FallsBackToLegacyDefault()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);

        var accessor = new CurrentTenantAccessor();
        var provider = new Mock<ITenantProvider>();
        provider.Setup(p => p.GetCurrentTenantId()).Returns("unknown-tenant");

        var service = new CurrentTenantService(
            provider.Object,
            accessor,
            db,
            NullLogger<CurrentTenantService>.Instance);

        await service.ApplyCurrentTenantAsync();

        Assert.Equal(LegacyDefaultTenantIds.Primary, accessor.TenantId);
    }
}
