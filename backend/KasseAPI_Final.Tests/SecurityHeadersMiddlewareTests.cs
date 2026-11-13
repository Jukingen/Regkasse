using KasseAPI_Final.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_sets_baseline_security_headers()
    {
        var context = new DefaultHttpContext();
        var middleware = CreateMiddleware(Environments.Development);

        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("1; mode=block", context.Response.Headers["X-XSS-Protection"].ToString());
        Assert.Equal(
            "strict-origin-when-cross-origin",
            context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal(
            "geolocation=(), microphone=(), camera=()",
            context.Response.Headers["Permissions-Policy"].ToString());
        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public async Task InvokeAsync_skips_Hsts_in_Staging()
    {
        var context = new DefaultHttpContext();
        var middleware = CreateMiddleware(Environments.Staging);

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_sets_Hsts_in_Production()
    {
        var context = new DefaultHttpContext();
        var middleware = CreateMiddleware(Environments.Production);

        await middleware.InvokeAsync(context);

        Assert.Equal(
            "max-age=31536000; includeSubDomains; preload",
            context.Response.Headers["Strict-Transport-Security"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_calls_next_delegate()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(
            Environments.Production,
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.True(nextCalled);
    }

    private static SecurityHeadersMiddleware CreateMiddleware(
        string environmentName,
        RequestDelegate? next = null)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(environmentName);

        return new SecurityHeadersMiddleware(
            next ?? (_ => Task.CompletedTask),
            env.Object);
    }
}
