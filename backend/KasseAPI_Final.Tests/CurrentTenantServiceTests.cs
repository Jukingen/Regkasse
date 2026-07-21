using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
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
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static (CurrentTenantService Service, CurrentTenantAccessor Accessor, DefaultHttpContext HttpContext) CreateHarness(
        AppDbContext db,
        Action<DefaultHttpContext>? configure = null)
    {
        var accessor = new CurrentTenantAccessor();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        var tenantContextService = new TenantContextService(
            db,
            accessor,
            environment.Object,
            Mock.Of<KasseAPI_Final.Services.Tenancy.ITenantDomainService>(),
            NullLogger<TenantContextService>.Instance);

        var httpContext = new DefaultHttpContext();
        configure?.Invoke(httpContext);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        var service = new CurrentTenantService(tenantContextService, httpContextAccessor.Object, environment.Object);
        return (service, accessor, httpContext);
    }

    [Fact]
    public async Task ApplyCurrentTenantAsync_ResolvesSlugToTenantGuid()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        db.Tenants.Add(new Tenant { Id = tenantB, Name = "B", Slug = "companyb" });
        await db.SaveChangesAsync();

        var (service, accessor, _) = CreateHarness(db, ctx =>
        {
            ctx.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "companyb";
        });

        await service.ApplyCurrentTenantAsync();

        Assert.Equal(tenantB, accessor.TenantId);
    }

    [Fact]
    public async Task ApplyCurrentTenantAsync_AdminSlug_MapsToDefaultTenant()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);

        var (service, accessor, _) = CreateHarness(db, ctx =>
        {
            ctx.Request.Host = new HostString("localhost");
        });

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

        var (service, accessor, _) = CreateHarness(db, ctx =>
        {
            ctx.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "test_cafe";
        });

        await service.ApplyCurrentTenantAsync();

        Assert.Equal(DemoTenantIds.Dev, accessor.TenantId);
    }

    [Fact]
    public async Task ApplyDevTenantOverrideAsync_Throws_OutsideDevelopment()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);

        var accessor = new CurrentTenantAccessor();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);

        var tenantContextService = new TenantContextService(
            db,
            accessor,
            environment.Object,
            Mock.Of<KasseAPI_Final.Services.Tenancy.ITenantDomainService>(),
            NullLogger<TenantContextService>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "dev";
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        var service = new CurrentTenantService(tenantContextService, httpContextAccessor.Object, environment.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyDevTenantOverrideAsync());
    }
}
