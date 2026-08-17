using System.Net;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PrometheusMetricsAccessTests
{
    [Fact]
    public void IsAllowed_loopback_always()
    {
        var options = new PrometheusMonitoringOptions { AllowedCidrs = ["10.0.0.0/8"] };
        Assert.True(PrometheusMetricsAccess.IsAllowed(IPAddress.Loopback, options));
        Assert.True(PrometheusMetricsAccess.IsAllowed(IPAddress.IPv6Loopback, options));
    }

    [Fact]
    public void IsAllowed_default_rfc1918_when_allowlist_empty()
    {
        var options = new PrometheusMonitoringOptions();
        Assert.True(PrometheusMetricsAccess.IsAllowed(IPAddress.Parse("10.1.2.3"), options));
        Assert.True(PrometheusMetricsAccess.IsAllowed(IPAddress.Parse("192.168.1.20"), options));
        Assert.False(PrometheusMetricsAccess.IsAllowed(IPAddress.Parse("8.8.8.8"), options));
    }

    [Fact]
    public void IsAllowed_explicit_cidr_replaces_rfc1918_default()
    {
        var options = new PrometheusMonitoringOptions { AllowedCidrs = ["198.51.100.0/24"] };
        Assert.True(PrometheusMetricsAccess.IsAllowed(IPAddress.Parse("198.51.100.10"), options));
        Assert.False(PrometheusMetricsAccess.IsAllowed(IPAddress.Parse("10.1.2.3"), options));
    }

    [Fact]
    public async Task Middleware_allows_metrics_in_Development_from_public_ip()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(Environments.Development, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/metrics";
        context.Connection.RemoteIpAddress = IPAddress.Parse("8.8.8.8");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_forbids_public_ip_in_Production()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(Environments.Production, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/metrics";
        context.Connection.RemoteIpAddress = IPAddress.Parse("8.8.8.8");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_allows_private_ip_in_Production()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(Environments.Production, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/metrics";
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Middleware_ignores_non_metrics_paths()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(Environments.Production, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/health/live";
        context.Connection.RemoteIpAddress = IPAddress.Parse("8.8.8.8");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    private static PrometheusMetricsAccessMiddleware CreateMiddleware(
        string environmentName,
        RequestDelegate next)
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        var monitoring = new MonitoringOptions
        {
            Enabled = true,
            MetricsEndpoint = "/metrics",
            Prometheus = new PrometheusMonitoringOptions { Enabled = true },
        };
        return new PrometheusMetricsAccessMiddleware(
            next,
            env.Object,
            new OptionsMonitorStub(monitoring),
            NullLogger<PrometheusMetricsAccessMiddleware>.Instance);
    }

    private sealed class OptionsMonitorStub : IOptionsMonitor<MonitoringOptions>
    {
        public OptionsMonitorStub(MonitoringOptions current) => CurrentValue = current;
        public MonitoringOptions CurrentValue { get; }
        public MonitoringOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<MonitoringOptions, string?> listener) => null;
    }
}
