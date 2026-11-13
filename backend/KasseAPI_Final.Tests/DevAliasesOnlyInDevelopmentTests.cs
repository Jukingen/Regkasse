using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// P0: DevTenantSlugAliases (cafe→dev, bar→prod) must apply only in Development.
/// </summary>
public sealed class DevAliasesOnlyInDevelopmentTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DevAliases_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static TenantContextService CreateService(
        AppDbContext db,
        ICurrentTenantAccessor accessor,
        bool isDevelopment)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(isDevelopment ? "Development" : "Production");

        return new TenantContextService(
            db,
            accessor,
            environment.Object,
            Mock.Of<ITenantDomainService>(),
            NullLogger<TenantContextService>.Instance);
    }

    [Fact]
    public async Task Development_CafeHost_AliasesToDev()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(db);
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
        var service = CreateService(db, accessor, isDevelopment: true);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("cafe.regkasse.local");

        await service.ApplyFromHostAsync(httpContext);

        Assert.Equal(DemoTenantIds.Dev, accessor.TenantId);
        Assert.Equal("dev", accessor.TenantSlug);
    }

    [Fact]
    public async Task Production_CafeHost_DoesNotAliasToDev_LeavesAmbientNull()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(db);
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
        var service = CreateService(db, accessor, isDevelopment: false);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("cafe.regkasse.at");

        await service.ApplyFromHostAsync(httpContext);

        // No tenant with slug "cafe" — exact lookup fails closed (not remapped to seeded "dev").
        Assert.Null(accessor.TenantId);
        Assert.Null(accessor.TenantSlug);
        Assert.NotEqual(DemoTenantIds.Dev, accessor.TenantId);
    }

    [Fact]
    public async Task Production_CafeHost_BindsRealCafeTenant_WhenPresent()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var cafeId = Guid.Parse("cafecafe-0001-4001-8001-000000000001");
        db.Tenants.Add(new Tenant
        {
            Id = cafeId,
            Name = "Cafe Mandant",
            Slug = "cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var accessor = new CurrentTenantAccessor();
        var service = CreateService(db, accessor, isDevelopment: false);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("cafe.regkasse.at");

        await service.ApplyFromHostAsync(httpContext);

        Assert.Equal(cafeId, accessor.TenantId);
        Assert.Equal("cafe", accessor.TenantSlug);
    }

    [Fact]
    public async Task Development_CafeHeader_AliasesToDev()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(db);
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
        var service = CreateService(db, accessor, isDevelopment: true);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "cafe";

        await service.ApplyFromRequestAsync(httpContext);

        Assert.Equal(DemoTenantIds.Dev, accessor.TenantId);
        Assert.Equal("dev", accessor.TenantSlug);
    }
}
