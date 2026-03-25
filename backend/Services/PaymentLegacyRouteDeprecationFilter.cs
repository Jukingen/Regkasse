using Microsoft.AspNetCore.Mvc.Filters;

namespace KasseAPI_Final.Services;

/// <summary>
/// Adds deprecation headers and telemetry for deprecated /api/Payment/* aliases.
/// Canonical boundary remains /api/pos/payment/* for POS and /api/admin/payments/* for admin.
/// </summary>
public sealed class PaymentLegacyRouteDeprecationFilter : IAsyncActionFilter
{
    private const string LegacyPrefix = "/api/Payment";
    private const string CanonicalPrefix = "/api/pos/payment";
    private const string SunsetDateRfc1123 = "Wed, 30 Sep 2026 23:59:59 GMT";

    private readonly ICoreMetrics _metrics;
    private readonly ILogger<PaymentLegacyRouteDeprecationFilter> _logger;

    public PaymentLegacyRouteDeprecationFilter(
        ICoreMetrics metrics,
        ILogger<PaymentLegacyRouteDeprecationFilter> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;
        if (path.StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var suffix = path.Length > LegacyPrefix.Length ? path[LegacyPrefix.Length..] : string.Empty;
            var canonicalPath = CanonicalPrefix + suffix;
            var responseHeaders = context.HttpContext.Response.Headers;
            responseHeaders["Deprecation"] = "true";
            responseHeaders["Sunset"] = SunsetDateRfc1123;
            responseHeaders["Link"] = $"<{canonicalPath}>; rel=\"successor-version\"";
            responseHeaders["X-Regkasse-Canonical-Route"] = canonicalPath;

            var routeTemplate = (context.ActionDescriptor.AttributeRouteInfo?.Template ?? string.Empty).Trim();
            _metrics.RecordLegacyPaymentRouteHit(
                string.IsNullOrWhiteSpace(routeTemplate) ? "unknown" : routeTemplate,
                context.HttpContext.Request.Method);
            _logger.LogWarning(
                "Deprecated payment route used: {LegacyPath} -> {CanonicalPath}, method={Method}",
                path,
                canonicalPath,
                context.HttpContext.Request.Method);
        }

        await next();
    }
}
