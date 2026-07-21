using System.Text;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantValidationMiddlewareTests
{
    private static DefaultHttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

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
    [InlineData("/api/admin/tenants/switcher")]
    [InlineData("/api/admin/billing/license-sales")]
    [InlineData("/api/admin/billing/stats")]
    public async Task InvokeAsync_SkipsSuperAdminPaths(string path)
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
    [InlineData("/api/pos/cart/current")]
    [InlineData("/api/admin/products")]
    [InlineData("/api/license/status")]
    public async Task InvokeAsync_Returns404_WhenTenantMissingOnProtectedPath(string path)
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

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        var body = await ReadBodyAsync(context);
        Assert.Contains("Not Found", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("The requested resource could not be found", body, StringComparison.OrdinalIgnoreCase);
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
}
