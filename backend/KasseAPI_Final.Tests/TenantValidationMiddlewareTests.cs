using System.Security.Claims;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantValidationMiddlewareTests
{
    private static DefaultHttpContext CreateHttpContext(string path, ClaimsPrincipal? user = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (user != null)
            context.User = user;
        return context;
    }

    private static ClaimsPrincipal SuperAdminPrincipal() =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "sa-1"),
            new Claim(ClaimTypes.Role, Roles.SuperAdmin),
        ],
        authenticationType: "Test"));

    private static ClaimsPrincipal ManagerPrincipal() =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "mgr-1"),
            new Claim(ClaimTypes.Role, Roles.Manager),
        ],
        authenticationType: "Test"));

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static TenantValidationMiddleware CreateSut(RequestDelegate next) =>
        new(next, NullLogger<TenantValidationMiddleware>.Instance);

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/refresh")]
    [InlineData("/api/auth/verify-2fa")]
    [InlineData("/api/Auth/me")]
    [InlineData("/api/auth/me")]
    [InlineData("/api/auth/logout")]
    [InlineData("/api/auth/forgot-password")]
    [InlineData("/api/auth/forgot-username")]
    [InlineData("/api/csrf/token")]
    [InlineData("/api/health")]
    [InlineData("/api/health/live")]
    [InlineData("/health")]
    [InlineData("/metrics")]
    [InlineData("/swagger/index.html")]
    public async Task InvokeAsync_SkipsPublicPaths(string path)
    {
        var accessor = new CurrentTenantAccessor { TenantId = null };
        var context = CreateHttpContext(path);
        var nextCalled = false;

        var sut = CreateSut(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(context, accessor);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/tenants")]
    [InlineData("/api/admin/tenants/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/impersonate")]
    [InlineData("/api/admin/billing/license-sales")]
    [InlineData("/api/admin/billing/stats")]
    [InlineData("/api/admin/cache/clear")]
    [InlineData("/api/admin/support/tickets/all")]
    [InlineData("/api/admin/support/admin/tickets")]
    [InlineData("/api/admin/trials")]
    [InlineData("/api/admin/trials/analytics")]
    [InlineData("/api/tenants/switcher")]
    public async Task SuperAdmin_CanAccessPlatformPaths_WithoutAmbientTenant(string path)
    {
        var accessor = new CurrentTenantAccessor { TenantId = null };
        var context = CreateHttpContext(path, SuperAdminPrincipal());
        var nextCalled = false;

        var sut = CreateSut(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(context, accessor);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/tenants")]
    [InlineData("/api/admin/billing/stats")]
    public async Task NonSuperAdmin_OnPlatformPath_WithoutTenant_Returns404(string path)
    {
        var accessor = new CurrentTenantAccessor { TenantId = null };
        var context = CreateHttpContext(path, ManagerPrincipal());
        var nextCalled = false;

        var sut = CreateSut(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(context, accessor);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/api/pos/cart/current")]
    [InlineData("/api/admin/products")]
    [InlineData("/api/license/status")]
    [InlineData("/api/tenants/current")]
    [InlineData("/api/admin/tenantsfoo")]
    public async Task InvokeAsync_Returns404_WhenTenantMissingOnProtectedPath(string path)
    {
        var accessor = new CurrentTenantAccessor { TenantId = null };
        var context = CreateHttpContext(path, SuperAdminPrincipal());
        var nextCalled = false;

        var sut = CreateSut(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(context, accessor);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        var body = await ReadBodyAsync(context);
        Assert.Contains("Not Found", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("The requested resource could not be found", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuperAdmin_CanAccessAllTenants_ButTenantContextRequired()
    {
        // Platform list: no ambient OK. Mandant data API: ambient required even for SuperAdmin.
        var accessor = new CurrentTenantAccessor { TenantId = null };
        var platformOk = false;
        var dataBlocked = false;

        await CreateSut(_ =>
        {
            platformOk = true;
            return Task.CompletedTask;
        }).InvokeAsync(CreateHttpContext("/api/admin/tenants", SuperAdminPrincipal()), accessor);

        await CreateSut(_ =>
        {
            dataBlocked = true;
            return Task.CompletedTask;
        }).InvokeAsync(CreateHttpContext("/api/admin/products", SuperAdminPrincipal()), accessor);

        Assert.True(platformOk);
        Assert.False(dataBlocked);
    }

    [Fact]
    public async Task SuperAdmin_NoTenantContext_BlockedForDataAccess()
    {
        var accessor = new CurrentTenantAccessor { TenantId = null };
        var context = CreateHttpContext("/api/admin/products", SuperAdminPrincipal());
        var nextCalled = false;

        await CreateSut(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }).InvokeAsync(context, accessor);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_AllowsRequest_WhenTenantPresent()
    {
        var accessor = new CurrentTenantAccessor { TenantId = Guid.NewGuid() };
        var context = CreateHttpContext("/api/pos/cart/current");
        var nextCalled = false;

        var sut = CreateSut(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(context, accessor);

        Assert.True(nextCalled);
    }

    [Theory]
    [InlineData("/api/admin/tenants", "/api/admin/tenants", true)]
    [InlineData("/api/admin/tenants/x", "/api/admin/tenants", true)]
    [InlineData("/api/admin/tenantsfoo", "/api/admin/tenants", false)]
    public void MatchesPathPrefix_IsSegmentSafe(string path, string prefix, bool expected)
    {
        Assert.Equal(expected, TenantValidationMiddleware.MatchesPathPrefix(path, prefix));
    }
}
