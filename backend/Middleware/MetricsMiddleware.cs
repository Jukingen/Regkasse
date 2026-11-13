using System.Diagnostics;
using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Logging;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Metrics;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.Extensions.Options;
using Prometheus;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Records HTTP API Prometheus metrics: <c>api_requests_total</c>, <c>api_request_duration_ms</c>,
/// <c>api_errors_total</c>, <c>api_active_requests</c>.
/// Also emits concise request duration (Debug), slow-request warnings, and enriched failure logs.
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
    private readonly MonitoringOptions _monitoringOptions;

    public MetricsMiddleware(
        RequestDelegate next,
        ILogger<MetricsMiddleware> logger,
        IOptions<MonitoringOptions>? monitoringOptions = null)
    {
        _next = next;
        _logger = logger;
        _monitoringOptions = monitoringOptions?.Value ?? new MonitoringOptions();
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
        Exception? caught = null;

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
            caught = ex;
            endpoint = ResolveEndpointLabel(context, endpoint);
            ErrorCounter.WithLabels(method, endpoint, ex.GetType().Name).Inc();
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            RequestDuration.WithLabels(method, endpoint).Observe(elapsedMs);
            accumulator.Record(elapsedMs, isError);
            ActiveRequests.Dec();

            LogRequestOutcome(context, method, endpoint, elapsedMs, caught);
        }
    }

    private void LogRequestOutcome(
        HttpContext context,
        string method,
        string endpoint,
        long elapsedMs,
        Exception? caught)
    {
        var statusCode = caught != null
            ? (context.Response.StatusCode >= 400 ? context.Response.StatusCode : 500)
            : context.Response.StatusCode;
        var pathAndQuery = $"{context.Request.Path}{context.Request.QueryString}";
        var (userLabel, userId, tenantLabel, tenantId) = ResolveActorLabels(context);

        if (caught != null)
        {
            _logger.LogError(
                caught,
                "API request failed: {Method} {PathAndQuery} - {StatusCode}\nUser: {User} ({UserId})\nTenant: {Tenant} ({TenantId})\nError: {ErrorType}: {ErrorMessage}",
                method,
                pathAndQuery,
                statusCode,
                userLabel,
                userId,
                tenantLabel,
                tenantId,
                caught.GetType().Name,
                caught.Message);
            return;
        }

        if (statusCode >= 500)
        {
            _logger.LogWarning(
                "API request failed: {Method} {PathAndQuery} - {StatusCode}\nUser: {User} ({UserId})\nTenant: {Tenant} ({TenantId})",
                method,
                pathAndQuery,
                statusCode,
                userLabel,
                userId,
                tenantLabel,
                tenantId);
        }

        var threshold = Math.Max(0, _monitoringOptions.SlowRequestThresholdMs);
        if (threshold > 0 && elapsedMs >= threshold)
        {
            _logger.LogWarning(
                "Slow request: {Method} {Path} - {StatusCode} - {Duration}ms (threshold {Threshold}ms) | User: {User} | Tenant: {Tenant}",
                method,
                context.Request.Path.Value,
                statusCode,
                elapsedMs,
                threshold,
                userLabel,
                tenantLabel);
        }
        else if (statusCode < 500)
        {
            _logger.LogDebug(
                "{Method} {Path} - {StatusCode} - {Duration}ms",
                method,
                context.Request.Path.Value,
                statusCode,
                elapsedMs);
        }
    }

    private static (string User, string UserId, string Tenant, string TenantId) ResolveActorLabels(HttpContext context)
    {
        var user = context.User;
        var email = user.GetActorEmail();
        var userIdRaw = user.GetActorUserId();
        var userLabel = string.IsNullOrWhiteSpace(email) ? "-" : email.Trim();
        var userId = string.IsNullOrWhiteSpace(userIdRaw) ? "-" : LogIdFormatting.ShortId(userIdRaw);

        var tenantLabel = "-";
        var tenantId = "-";
        var requestServices = context.RequestServices;
        if (requestServices != null)
        {
            var tenantAccessor = requestServices.GetService<ICurrentTenantAccessor>();
            if (tenantAccessor != null)
            {
                if (!string.IsNullOrWhiteSpace(tenantAccessor.TenantSlug))
                    tenantLabel = tenantAccessor.TenantSlug.Trim();
                if (tenantAccessor.TenantId is Guid tid && tid != Guid.Empty)
                    tenantId = LogIdFormatting.ShortGuid(tid);
            }
        }

        if (tenantId == "-" && user?.Identity?.IsAuthenticated == true)
        {
            var claim = user.FindFirst(ScopeCheckService.TenantIdClaim)?.Value;
            if (!string.IsNullOrWhiteSpace(claim))
                tenantId = LogIdFormatting.ShortId(claim);
        }

        return (userLabel, userId, tenantLabel, tenantId);
    }

    internal static bool IsExemptPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return true;

        return path.Equals("/metrics", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/health", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/health/", StringComparison.OrdinalIgnoreCase)
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
