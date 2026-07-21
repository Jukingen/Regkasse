using KasseAPI_Final.Data;
using KasseAPI_Final.Middleware;
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

public sealed class TenantResolutionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_Development_DevHeader_BindsDevTenant()
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
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        var tenantContextService = new TenantContextService(
            db,
            accessor,
            environment.Object,
            Mock.Of<KasseAPI_Final.Services.Tenancy.ITenantDomainService>(),
            NullLogger<TenantContextService>.Instance);

        var httpContextAccessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "dev";
        httpContextAccessor.HttpContext = httpContext;

        var currentTenantService = new CurrentTenantService(tenantContextService, httpContextAccessor, environment.Object);
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask, environment.Object);

        await middleware.InvokeAsync(httpContext, currentTenantService, accessor);

        Assert.Equal(DemoTenantIds.Dev, accessor.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_Development_NoDevHeader_UsesHostSlug()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);
        await db.SaveChangesAsync();

        var accessor = new CurrentTenantAccessor();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        var tenantContextService = new TenantContextService(
            db,
            accessor,
            environment.Object,
            Mock.Of<KasseAPI_Final.Services.Tenancy.ITenantDomainService>(),
            NullLogger<TenantContextService>.Instance);

        var httpContextAccessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("localhost");
        httpContextAccessor.HttpContext = httpContext;

        var currentTenantService = new CurrentTenantService(tenantContextService, httpContextAccessor, environment.Object);
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask, environment.Object);

        await middleware.InvokeAsync(httpContext, currentTenantService, accessor);

        Assert.Equal(LegacyDefaultTenantIds.Primary, accessor.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_Production_ApiHost_LeavesAmbientTenantUnset()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);
        await db.SaveChangesAsync();

        var accessor = new CurrentTenantAccessor { TenantId = Guid.NewGuid() };
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);

        var tenantContextService = new TenantContextService(
            db,
            accessor,
            environment.Object,
            Mock.Of<KasseAPI_Final.Services.Tenancy.ITenantDomainService>(),
            NullLogger<TenantContextService>.Instance);

        var httpContextAccessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("api.regkasse.at");
        httpContext.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "dev";
        httpContextAccessor.HttpContext = httpContext;

        // Pre-set ambient must be cleared on platform-host skip (no JWT yet).
        accessor.TenantId = Guid.NewGuid();

        var currentTenantService = new CurrentTenantService(tenantContextService, httpContextAccessor, environment.Object);
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask, environment.Object);

        await middleware.InvokeAsync(httpContext, currentTenantService, accessor);

        Assert.Null(accessor.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_Production_MandantSubdomain_BindsFromHost()
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
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);

        var tenantContextService = new TenantContextService(
            db,
            accessor,
            environment.Object,
            Mock.Of<KasseAPI_Final.Services.Tenancy.ITenantDomainService>(),
            NullLogger<TenantContextService>.Instance);

        var httpContextAccessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("dev.regkasse.at");
        httpContextAccessor.HttpContext = httpContext;

        var currentTenantService = new CurrentTenantService(tenantContextService, httpContextAccessor, environment.Object);
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask, environment.Object);

        await middleware.InvokeAsync(httpContext, currentTenantService, accessor);

        Assert.Equal(DemoTenantIds.Dev, accessor.TenantId);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantResolutionMiddleware_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }
}
