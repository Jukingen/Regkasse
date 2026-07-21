using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Services.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests.Security;

public sealed class CsrfMiddlewareTests
{
    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/Auth/login")]
    [InlineData("/api/auth/refresh")]
    [InlineData("/api/Auth/refresh")]
    [InlineData("/api/auth/verify-2fa")]
    [InlineData("/api/Auth/verify-2fa")]
    [InlineData("/health")]
    [InlineData("/api/health")]
    [InlineData("/swagger")]
    [InlineData("/metrics")]
    [InlineData("/api/webhooks")]
    [InlineData("/api/webhooks/stripe")]
    [InlineData("/api/csrf/token")]
    public void IsExemptPath_skips_public_and_ops_endpoints(string path)
    {
        Assert.True(CsrfMiddleware.IsExemptPath(path));
    }

    [Theory]
    [InlineData("/api/admin/tenants")]
    [InlineData("/api/pos/payment")]
    [InlineData("/api/Auth/logout")]
    public void IsExemptPath_does_not_skip_app_paths(string path)
    {
        Assert.False(CsrfMiddleware.IsExemptPath(path));
    }

    [Fact]
    public async Task InvokeAsync_skips_when_disabled()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(
            envName: Environments.Production,
            enabled: false,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        var context = CreateContext("POST", "/api/admin/tenants");
        await middleware.InvokeAsync(context, CreateCsrfService());

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_skips_in_Development_when_bypass_enabled()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(
            envName: Environments.Development,
            enabled: true,
            bypassInDevelopment: true,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        var context = CreateContext("POST", "/api/admin/tenants");
        await middleware.InvokeAsync(context, CreateCsrfService());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_enforces_in_Development_when_bypass_disabled()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(
            envName: Environments.Development,
            enabled: true,
            bypassInDevelopment: false,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        var context = CreateContext("POST", "/api/admin/tenants");
        await middleware.InvokeAsync(context, CreateCsrfService());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Production_rejects_POST_without_token()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(
            envName: Environments.Production,
            enabled: true,
            bypassInDevelopment: false,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        var context = CreateContext("POST", "/api/admin/tenants");
        await middleware.InvokeAsync(context, CreateCsrfService());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(
            "Invalid CSRF token. Please refresh the page and try again.",
            doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task InvokeAsync_Production_allows_POST_with_valid_token()
    {
        var csrf = CreateCsrfService();
        var token = csrf.GenerateToken();
        var nextCalled = false;
        var middleware = CreateMiddleware(
            envName: Environments.Production,
            enabled: true,
            bypassInDevelopment: false,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        var context = CreateContext("POST", "/api/admin/tenants");
        context.Request.Headers["X-XSRF-TOKEN"] = token;
        context.Request.Headers.Cookie = $"XSRF-TOKEN={token}";

        await middleware.InvokeAsync(context, csrf);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_skips_login_without_token()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(
            envName: Environments.Production,
            enabled: true,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        var context = CreateContext("POST", "/api/Auth/login");
        await middleware.InvokeAsync(context, CreateCsrfService());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_allows_GET_without_token()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(
            envName: Environments.Production,
            enabled: true,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        var context = CreateContext("GET", "/api/admin/tenants");
        await middleware.InvokeAsync(context, CreateCsrfService());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_allows_native_mirror_header_when_cookie_absent()
    {
        var csrf = CreateCsrfService();
        var token = csrf.GenerateToken();
        var nextCalled = false;
        var middleware = CreateMiddleware(
            envName: Environments.Production,
            enabled: true,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        var context = CreateContext("POST", "/api/pos/payment");
        context.Request.Headers["X-XSRF-TOKEN"] = token;
        context.Request.Headers["X-CSRF-COOKIE"] = token;

        await middleware.InvokeAsync(context, csrf);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_skips_exempt_webhook_without_token()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(
            envName: Environments.Production,
            enabled: true,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        var context = CreateContext("POST", "/api/webhooks/stripe");
        await middleware.InvokeAsync(context, CreateCsrfService());

        Assert.True(nextCalled);
    }

    private static CsrfMiddleware CreateMiddleware(
        string envName,
        bool enabled,
        RequestDelegate next,
        bool bypassInDevelopment = true)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(envName);

        var monitor = new OptionsMonitorStub(new CsrfOptions
        {
            Enabled = enabled,
            BypassInDevelopment = bypassInDevelopment,
            HeaderName = "X-XSRF-TOKEN",
            CookieName = "XSRF-TOKEN",
        });

        return new CsrfMiddleware(
            next,
            NullLogger<CsrfMiddleware>.Instance,
            env.Object,
            monitor);
    }

    private static ICsrfTokenService CreateCsrfService()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var monitor = new OptionsMonitorStub(new CsrfOptions { TokenLifetimeHours = 24 });
        return new CsrfTokenService(cache, NullLogger<CsrfTokenService>.Instance, monitor);
    }

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class OptionsMonitorStub : IOptionsMonitor<CsrfOptions>
    {
        public OptionsMonitorStub(CsrfOptions current) => CurrentValue = current;

        public CsrfOptions CurrentValue { get; }

        public CsrfOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<CsrfOptions, string?> listener) =>
            new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
