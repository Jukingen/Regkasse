using System.Security.Claims;
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

public sealed class TenantContextMiddlewareTests
{
    [Fact]
    public void HasDevTenantOverride_True_When_Header_Present()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "dev";

        Assert.True(TenantContextMiddleware.HasDevTenantOverride(context));
    }

    [Fact]
    public void HasDevTenantOverride_True_When_Query_Present()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tenant=bar");

        Assert.True(TenantContextMiddleware.HasDevTenantOverride(context));
    }

    [Fact]
    public void HasDevTenantOverride_False_When_No_Dev_Override()
    {
        var context = new DefaultHttpContext();

        Assert.False(TenantContextMiddleware.HasDevTenantOverride(context));
    }

    [Fact]
    public void HasDevTenantOverride_False_When_Header_Is_Platform_Admin()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "admin";

        Assert.False(TenantContextMiddleware.HasDevTenantOverride(context));
    }

    [Fact]
    public void HasDevTenantOverride_False_When_Query_Is_Platform_Admin()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tenant=admin");

        Assert.False(TenantContextMiddleware.HasDevTenantOverride(context));
    }

    [Fact]
    public async Task InvokeAsync_Development_DevHeader_WinsOverJwtTenantId()
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

        var accessor = new CurrentTenantAccessor { TenantId = LegacyDefaultTenantIds.Primary };
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        var tenantContextService = new TenantContextService(
            db,
            accessor,
            environment.Object,
            Mock.Of<KasseAPI_Final.Services.Tenancy.ITenantDomainService>(),
            NullLogger<TenantContextService>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "dev";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", LegacyDefaultTenantIds.Primary.ToString("D")),
        ],
        authenticationType: "Test"));

        var middleware = new TenantContextMiddleware(
            _ => Task.CompletedTask,
            environment.Object);

        await middleware.InvokeAsync(httpContext, tenantContextService);

        Assert.Equal(DemoTenantIds.Dev, accessor.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_Production_JwtTenantId_UsedWhenAuthenticated()
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

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "dev";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", LegacyDefaultTenantIds.Primary.ToString("D")),
        ],
        authenticationType: "Test"));

        var middleware = new TenantContextMiddleware(
            _ => Task.CompletedTask,
            environment.Object);

        await middleware.InvokeAsync(httpContext, tenantContextService);

        Assert.Equal(LegacyDefaultTenantIds.Primary, accessor.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_Production_MissingJwtTenantId_ClearsAmbientTenant()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);

        var accessor = new CurrentTenantAccessor { TenantId = LegacyDefaultTenantIds.Primary };
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);

        var tenantContextService = new TenantContextService(
            db,
            accessor,
            environment.Object,
            Mock.Of<KasseAPI_Final.Services.Tenancy.ITenantDomainService>(),
            NullLogger<TenantContextService>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("api.regkasse.at");
        httpContext.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "dev";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "user"),
        ],
        authenticationType: "Test"));

        var middleware = new TenantContextMiddleware(
            _ => Task.CompletedTask,
            environment.Object);

        await middleware.InvokeAsync(httpContext, tenantContextService);

        Assert.Null(accessor.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_Development_AdminHeader_DoesNotOverrideJwtDevTenant()
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

        var accessor = new CurrentTenantAccessor { TenantId = LegacyDefaultTenantIds.Primary };
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        var tenantContextService = new TenantContextService(
            db,
            accessor,
            environment.Object,
            Mock.Of<KasseAPI_Final.Services.Tenancy.ITenantDomainService>(),
            NullLogger<TenantContextService>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "admin";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", DemoTenantIds.Dev.ToString("D")),
        ],
        authenticationType: "Test"));

        var middleware = new TenantContextMiddleware(
            _ => Task.CompletedTask,
            environment.Object);

        await middleware.InvokeAsync(httpContext, tenantContextService);

        Assert.Equal(DemoTenantIds.Dev, accessor.TenantId);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantContextMiddleware_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }
}
