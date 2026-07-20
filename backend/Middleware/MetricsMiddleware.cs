using System.Diagnostics;
using System.Text.RegularExpressions;
using KasseAPI_Final.Services.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Records HTTP API Prometheus metrics: <c>api_requests_total</c>, <c>api_request_duration_ms</c>,
/// <c>api_errors_total</c>, <c>api_active_requests</c>.
/// </summary>
public class MetricsMiddleware
{
    private static readonly Regex GuidSegment = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NumericSegment = new(
        @"(?<=/)\d+(?=/|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Counter RequestCounter = Metrics
        .CreateCounter("api_requests_total", "Total API requests",
            new CounterConfiguration
            {
                LabelNames = ["method", "endpoint", "status_code"]
            });

    private static readonly Histogram RequestDuration = Metrics
        .CreateHistogram("api_request_duration_ms", "API request duration in milliseconds",
            new HistogramConfiguration
            {
                LabelNames = ["method", "endpoint"],
                Buckets = [10, 50, 100, 200, 500, 1000, 2000, 5000]
            });

    private static readonly Counter ErrorCounter = Metrics
        .CreateCounter("api_errors_total", "Total API errors",
            new CounterConfiguration
            {
                LabelNames = ["method", "endpoint", "error_type"]
            });

    private static readonly Gauge ActiveRequests = Metrics
        .CreateGauge("api_active_requests", "Currently active requests");

    private readonly RequestDelegate _next;
    private readonly ILogger<MetricsMiddleware> _logger;

    public MetricsMiddleware(RequestDelegate next, ILogger<MetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApiRequestMetricsAccumulator accumulator)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (IsExemptPath(path))
        {
            await _next(context);
            return;
        }

        var method = context.Request.Method;
        var endpoint = NormalizePathForMetric(path);
        var isError = false;

        ActiveRequests.Inc();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);

            endpoint = ResolveEndpointLabel(context, endpoint);
            var statusCode = context.Response.StatusCode.ToString();
            RequestCounter.WithLabels(method, endpoint, statusCode).Inc();

            if (context.Response.StatusCode >= 400)
            {
                isError = true;
                ErrorCounter.WithLabels(method, endpoint, statusCode).Inc();
            }
        }
        catch (Exception ex)
        {
            isError = true;
            endpoint = ResolveEndpointLabel(context, endpoint);
            ErrorCounter.WithLabels(method, endpoint, ex.GetType().Name).Inc();
            _logger.LogWarning(ex, "API request failed: {Method} {Endpoint}", method, endpoint);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            RequestDuration.WithLabels(method, endpoint).Observe(stopwatch.ElapsedMilliseconds);
            accumulator.Record(stopwatch.ElapsedMilliseconds, isError);
            ActiveRequests.Dec();
        }
    }

    internal static bool IsExemptPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return true;

        return path.Equals("/metrics", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/health", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/health/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }

    internal static string ResolveEndpointLabel(HttpContext context, string fallback)
    {
        if (context.GetEndpoint() is RouteEndpoint routeEndpoint)
        {
            var raw = routeEndpoint.RoutePattern.RawText;
            if (!string.IsNullOrWhiteSpace(raw))
                return raw.StartsWith('/') ? raw : "/" + raw;
        }

        return fallback;
    }

    /// <summary>Reduces Prometheus label cardinality by replacing GUID/numeric path segments.</summary>
    internal static string NormalizePathForMetric(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return "/";

        var normalized = GuidSegment.Replace(path, "{id}");
        normalized = NumericSegment.Replace(normalized, "{id}");
        return normalized;
    }
}
