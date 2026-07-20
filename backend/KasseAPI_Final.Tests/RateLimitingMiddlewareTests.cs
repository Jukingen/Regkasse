using System.Net;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RateLimitingMiddlewareTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/finanzonline")]
    [InlineData("/api/health")]
    [InlineData("/api/health/license")]
    [InlineData("/swagger")]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/metrics")]
    public void IsExemptPath_skips_ops_endpoints(string path)
    {
        Assert.True(RateLimitingMiddleware.IsExemptPath(path));
    }

    [Theory]
    [InlineData("/api/Auth/login")]
    [InlineData("/api/admin/tenants")]
    [InlineData("/")]
    public void IsExemptPath_does_not_skip_app_paths(string path)
    {
        Assert.False(RateLimitingMiddleware.IsExemptPath(path));
    }

    [Fact]
    public async Task InvokeAsync_skips_in_Development()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(
            envName: Environments.Development,
            enabled: true,
            limit: 1,
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        var context = CreateContext("/api/Auth/login");
        await middleware.InvokeAsync(context);
        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_skips_when_disabled()
    {
        var callCount = 0;
        var middleware = CreateMiddleware(
            envName: Environments.Production,
            enabled: false,
            limit: 1,
            next: _ =>
            {
                callCount++;
                return Task.CompletedTask;
            });

        var context = CreateContext("/api/Auth/login");
        await middleware.InvokeAsync(context);
        await middleware.InvokeAsync(context);

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task InvokeAsync_allows_within_limit()
    {
        var callCount = 0;
        var middleware = CreateMiddleware(
            envName: Environments.Production,
            enabled: true,
            limit: 3,
            next: _ =>
            {
                callCount++;
                return Task.CompletedTask;
            });

        for (var i = 0; i < 3; i++)
        {
            var context = CreateContext("/api/Auth/login");
            await middleware.InvokeAsync(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task InvokeAsync_returns_429_when_limit_exceeded()
    {
        var callCount = 0;
        var middleware = CreateMiddleware(
            envName: Environments.Production,
            enabled: true,
            limit: 2,
            windowSeconds: 60,
            next: _ =>
            {
                callCount++;
                return Task.CompletedTask;
            });

        await middleware.InvokeAsync(CreateContext("/api/Auth/login"));
        await middleware.InvokeAsync(CreateContext("/api/Auth/login"));

        var blocked = CreateContext("/api/Auth/login");
        blocked.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(blocked);

        Assert.Equal(2, callCount);
        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
        Assert.Equal("60", blocked.Response.Headers.RetryAfter.ToString());

        blocked.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(blocked.Response.Body);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(60, doc.RootElement.GetProperty("retryAfter").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_does_not_rate_limit_health_when_enabled()
    {
        var callCount = 0;
        var middleware = CreateMiddleware(
            envName: Environments.Production,
            enabled: true,
            limit: 1,
            next: _ =>
            {
                callCount++;
                return Task.CompletedTask;
            });

        await middleware.InvokeAsync(CreateContext("/health"));
        await middleware.InvokeAsync(CreateContext("/health"));

        Assert.Equal(2, callCount);
    }

    private static RateLimitingMiddleware CreateMiddleware(
        string envName,
        bool enabled,
        int limit,
        RequestDelegate next,
        int windowSeconds = 60)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(envName);

        var monitor = new OptionsMonitorStub(new RateLimitingOptions
        {
            Enabled = enabled,
            Limit = limit,
            WindowSeconds = windowSeconds
        });

        return new RateLimitingMiddleware(
            next,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<RateLimitingMiddleware>.Instance,
            env.Object,
            monitor);
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class OptionsMonitorStub : IOptionsMonitor<RateLimitingOptions>
    {
        public OptionsMonitorStub(RateLimitingOptions current) => CurrentValue = current;

        public RateLimitingOptions CurrentValue { get; }

        public RateLimitingOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<RateLimitingOptions, string?> listener) => null;
    }
}
