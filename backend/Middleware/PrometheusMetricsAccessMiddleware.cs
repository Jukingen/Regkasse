using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Restricts Prometheus scrape to loopback / configured CIDRs. Skipped in Development.
/// JWT is not used: scrapers have no user token.
/// </summary>
public sealed class PrometheusMetricsAccessMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<MonitoringOptions> _options;
    private readonly ILogger<PrometheusMetricsAccessMiddleware> _logger;

    public PrometheusMetricsAccessMiddleware(
        RequestDelegate next,
        IHostEnvironment environment,
        IOptionsMonitor<MonitoringOptions> options,
        ILogger<PrometheusMetricsAccessMiddleware> logger)
    {
        _next = next;
        _environment = environment;
        _options = options;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var monitoring = _options.CurrentValue;
        if (_environment.IsDevelopment()
            || !monitoring.Enabled
            || !monitoring.Prometheus.Enabled)
        {
            await _next(context);
            return;
        }

        var metricsPath = string.IsNullOrWhiteSpace(monitoring.MetricsEndpoint)
            ? "/metrics"
            : monitoring.MetricsEndpoint.Trim();
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.Equals(metricsPath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var remote = context.Connection.RemoteIpAddress;
        if (PrometheusMetricsAccess.IsAllowed(remote, monitoring.Prometheus))
        {
            await _next(context);
            return;
        }

        _logger.LogWarning(
            "Rejected /metrics scrape from {IP}",
            remote);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
    }
}
