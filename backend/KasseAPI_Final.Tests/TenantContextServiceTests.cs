using System.Security.Claims;
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

public sealed class TenantContextServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantContext_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static TenantContextService CreateService(
        AppDbContext db,
        ICurrentTenantAccessor accessor,
        bool isDevelopment = true)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(isDevelopment ? "Development" : "Production");

        return new TenantContextService(
            db,
            accessor,
            environment.Object,
            Mock.Of<KasseAPI_Final.Services.Tenancy.ITenantDomainService>(),
            NullLogger<TenantContextService>.Instance);
    }

    [Fact]
    public async Task ResolveTenantContextAsync_JwtTenant_WinsOverDevHeaderInDevelopment()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);
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
        var service = CreateService(db, accessor);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "dev";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", LegacyDefaultTenantIds.Primary.ToString("D")),
        ],
        authenticationType: "Test"));

        var resolved = await service.ResolveTenantContextAsync(httpContext);

        Assert.Equal(LegacyDefaultTenantIds.Primary, resolved.Id);
        Assert.Equal("default", resolved.Slug);
    }

    [Fact]
    public async Task ResolveTenantContextAsync_DevHeader_UsedWhenJwtMissing()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);
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
        var service = CreateService(db, accessor);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "dev";

        var resolved = await service.ResolveTenantContextAsync(httpContext);

        Assert.Equal(DemoTenantIds.Dev, resolved.Id);
        Assert.Equal("dev", resolved.Slug);
        Assert.Equal("Development", resolved.Name);
    }

    [Fact]
    public async Task ResolveTenantContextAsync_JwtTenant_UsedWhenNoDevOverride()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);
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
        httpContext.Request.Host = new HostString("localhost");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", DemoTenantIds.Dev.ToString("D")),
        ],
        authenticationType: "Test"));

        var resolved = await service.ResolveTenantContextAsync(httpContext);

        Assert.Equal(DemoTenantIds.Dev, resolved.Id);
        Assert.Equal("dev", resolved.Slug);
    }

    [Fact]
    public async Task ApplyAuthenticatedTenantAsync_Production_IgnoresDevHeader_UsesJwtOnly()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);
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
        httpContext.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "dev";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", LegacyDefaultTenantIds.Primary.ToString("D")),
        ],
        authenticationType: "Test"));

        await service.ApplyAuthenticatedTenantAsync(httpContext);

        Assert.Equal(LegacyDefaultTenantIds.Primary, accessor.TenantId);
    }

    [Fact]
    public async Task ApplyAuthenticatedTenantAsync_Production_MissingJwt_ClearsAmbient()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);

        var accessor = new CurrentTenantAccessor { TenantId = LegacyDefaultTenantIds.Primary };
        var service = CreateService(db, accessor, isDevelopment: false);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("dev.regkasse.at");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "user"),
        ],
        authenticationType: "Test"));

        await service.ApplyAuthenticatedTenantAsync(httpContext);

        Assert.Null(accessor.TenantId);
    }

    [Fact]
    public async Task ApplyFromRequestAsync_AdminHost_MapsToLegacyDefaultTenant()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);

        var accessor = new CurrentTenantAccessor();
        var service = CreateService(db, accessor);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("localhost");

        await service.ApplyFromRequestAsync(httpContext);

        Assert.Equal(LegacyDefaultTenantIds.Primary, accessor.TenantId);
    }

    [Fact]
    public async Task ApplyFromRequestAsync_LegacyAlias_ResolvesDevTenant()
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
        var service = CreateService(db, accessor);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "test_cafe";

        await service.ApplyFromRequestAsync(httpContext);

        Assert.Equal(DemoTenantIds.Dev, accessor.TenantId);
    }

    [Fact]
    public async Task ResolveTenantContextAsync_AdminHost_FallsBackToDefaultTenant()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);
        await db.SaveChangesAsync();

        var accessor = new CurrentTenantAccessor();
        var service = CreateService(db, accessor);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("localhost");

        var resolved = await service.ResolveTenantContextAsync(httpContext);

        Assert.Equal(LegacyDefaultTenantIds.Primary, resolved.Id);
        Assert.Equal("default", resolved.Slug);
    }
}
