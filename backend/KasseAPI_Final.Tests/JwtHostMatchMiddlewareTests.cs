using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// P2: JWT tenant_id must match Host-resolved tenant on mandant/custom hosts (Production).
/// </summary>
public sealed class JwtHostMatchMiddlewareTests
{
    private static readonly Guid CafeTenantId = Guid.Parse("cafecafe-0001-4001-8001-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("b0b0b0b0-0002-4001-8001-000000000002");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"JwtHostMatch_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static async Task<(TenantContextService Service, CurrentTenantAccessor Accessor, IWebHostEnvironment Env)> CreateProductionStackAsync(
        AppDbContext db)
    {
        TenantTestDoubles.EnsurePlatformTenant(db);
        db.Tenants.Add(new Tenant
        {
            Id = CafeTenantId,
            Name = "Cafe",
            Slug = "cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        db.Tenants.Add(new Tenant
        {
            Id = OtherTenantId,
            Name = "Other",
            Slug = "other",
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var accessor = new CurrentTenantAccessor();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);

        var service = new TenantContextService(
            db,
            accessor,
            environment.Object,
            Mock.Of<ITenantDomainService>(),
            NullLogger<TenantContextService>.Instance);

        return (service, accessor, environment.Object);
    }

    [Fact]
    public async Task JWT_MatchesHost_Allowed()
    {
        await using var db = CreateContext();
        var (service, accessor, env) = await CreateProductionStackAsync(db);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("cafe.regkasse.at");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ScopeCheckService.TenantIdClaim, CafeTenantId.ToString("D")),
        ],
        authenticationType: "Test"));

        var nextHit = false;
        var middleware = new TenantContextMiddleware(
            _ =>
            {
                nextHit = true;
                return Task.CompletedTask;
            },
            env);

        await middleware.InvokeAsync(
            httpContext,
            service,
            Options.Create(new AuthOptions { RequireTenantHostMatch = true }),
            NullLogger<TenantContextMiddleware>.Instance);

        Assert.True(nextHit);
        Assert.NotEqual(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(CafeTenantId, accessor.TenantId);
    }

    [Fact]
    public async Task JWT_MismatchHost_Blocked_403()
    {
        await using var db = CreateContext();
        var (service, accessor, env) = await CreateProductionStackAsync(db);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Request.Host = new HostString("cafe.regkasse.at");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ScopeCheckService.TenantIdClaim, OtherTenantId.ToString("D")),
        ],
        authenticationType: "Test"));

        var nextHit = false;
        var middleware = new TenantContextMiddleware(
            _ =>
            {
                nextHit = true;
                return Task.CompletedTask;
            },
            env);

        await middleware.InvokeAsync(
            httpContext,
            service,
            Options.Create(new AuthOptions { RequireTenantHostMatch = true }),
            NullLogger<TenantContextMiddleware>.Instance);

        Assert.False(nextHit);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Null(accessor.TenantId);

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("TENANT_HOST_MISMATCH", body, StringComparison.Ordinal);
        Assert.Contains("Tenant mismatch between authentication and host", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SharedHost_NoHostCheck()
    {
        await using var db = CreateContext();
        var (service, accessor, env) = await CreateProductionStackAsync(db);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("api.regkasse.at");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ScopeCheckService.TenantIdClaim, OtherTenantId.ToString("D")),
        ],
        authenticationType: "Test"));

        var nextHit = false;
        var middleware = new TenantContextMiddleware(
            _ =>
            {
                nextHit = true;
                return Task.CompletedTask;
            },
            env);

        await middleware.InvokeAsync(
            httpContext,
            service,
            Options.Create(new AuthOptions { RequireTenantHostMatch = true }),
            NullLogger<TenantContextMiddleware>.Instance);

        Assert.True(nextHit);
        Assert.Equal(OtherTenantId, accessor.TenantId);
    }

    [Fact]
    public async Task Localhost_NoHostCheck()
    {
        await using var db = CreateContext();
        var (service, accessor, env) = await CreateProductionStackAsync(db);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("localhost");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ScopeCheckService.TenantIdClaim, OtherTenantId.ToString("D")),
        ],
        authenticationType: "Test"));

        var nextHit = false;
        var middleware = new TenantContextMiddleware(
            _ =>
            {
                nextHit = true;
                return Task.CompletedTask;
            },
            env);

        await middleware.InvokeAsync(
            httpContext,
            service,
            Options.Create(new AuthOptions { RequireTenantHostMatch = true }),
            NullLogger<TenantContextMiddleware>.Instance);

        Assert.True(TenantContextMiddleware.IsSharedHost(httpContext.Request.Host));
        Assert.True(nextHit);
        Assert.Equal(OtherTenantId, accessor.TenantId);
    }

    [Fact]
    public async Task SuperAdminImpersonation_BypassesHostCheck()
    {
        await using var db = CreateContext();
        var (service, accessor, env) = await CreateProductionStackAsync(db);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("cafe.regkasse.at");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, Roles.SuperAdmin),
            new Claim(ImpersonationAuditContext.ImpersonationClaimType, "true"),
            new Claim(ScopeCheckService.TenantIdClaim, OtherTenantId.ToString("D")),
        ],
        authenticationType: "Test"));

        var nextHit = false;
        var middleware = new TenantContextMiddleware(
            _ =>
            {
                nextHit = true;
                return Task.CompletedTask;
            },
            env);

        await middleware.InvokeAsync(
            httpContext,
            service,
            Options.Create(new AuthOptions { RequireTenantHostMatch = true }),
            NullLogger<TenantContextMiddleware>.Instance);

        Assert.True(nextHit);
        Assert.Equal(OtherTenantId, accessor.TenantId);
    }

    [Fact]
    public async Task Development_NoHostCheck()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(db);
        db.Tenants.Add(new Tenant
        {
            Id = CafeTenantId,
            Name = "Cafe",
            Slug = "cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        db.Tenants.Add(new Tenant
        {
            Id = OtherTenantId,
            Name = "Other",
            Slug = "other",
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var accessor = new CurrentTenantAccessor();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        var service = new TenantContextService(
            db,
            accessor,
            environment.Object,
            Mock.Of<ITenantDomainService>(),
            NullLogger<TenantContextService>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("cafe.regkasse.at");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ScopeCheckService.TenantIdClaim, OtherTenantId.ToString("D")),
        ],
        authenticationType: "Test"));

        var nextHit = false;
        var middleware = new TenantContextMiddleware(
            _ =>
            {
                nextHit = true;
                return Task.CompletedTask;
            },
            environment.Object);

        // Even with RequireTenantHostMatch=true, Development skips the check.
        await middleware.InvokeAsync(
            httpContext,
            service,
            Options.Create(new AuthOptions { RequireTenantHostMatch = true }),
            NullLogger<TenantContextMiddleware>.Instance);

        Assert.True(nextHit);
        Assert.Equal(OtherTenantId, accessor.TenantId);
    }

    [Fact]
    public async Task ConfigFlagDisabled_NoHostCheck()
    {
        await using var db = CreateContext();
        var (service, accessor, env) = await CreateProductionStackAsync(db);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("cafe.regkasse.at");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ScopeCheckService.TenantIdClaim, OtherTenantId.ToString("D")),
        ],
        authenticationType: "Test"));

        var nextHit = false;
        var middleware = new TenantContextMiddleware(
            _ =>
            {
                nextHit = true;
                return Task.CompletedTask;
            },
            env);

        await middleware.InvokeAsync(
            httpContext,
            service,
            Options.Create(new AuthOptions { RequireTenantHostMatch = false }),
            NullLogger<TenantContextMiddleware>.Instance);

        Assert.True(nextHit);
        Assert.NotEqual(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(OtherTenantId, accessor.TenantId);
    }
}
