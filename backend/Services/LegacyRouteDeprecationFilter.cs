using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KasseAPI_Final.Services;

/// <summary>
/// Deprecation headers, warning logs, and Prometheus metrics for legacy API path aliases
/// (<c>/api/Payment</c>, <c>/api/Cart</c>, <c>/api/Product</c>). Canonical successors: <c>/api/pos/payment</c>, <c>/api/pos/cart</c>, <c>/api/pos</c>.
/// </summary>
public sealed class LegacyRouteDeprecationFilter : IAsyncActionFilter
{
    private const string SunsetDateRfc1123 = "Wed, 30 Sep 2026 23:59:59 GMT";

    private static readonly Regex GuidSegment = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    private readonly ICoreMetrics _metrics;
    private readonly ILogger<LegacyRouteDeprecationFilter> _logger;

    public LegacyRouteDeprecationFilter(ICoreMetrics metrics, ILogger<LegacyRouteDeprecationFilter> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;
        if (!TryGetLegacyInfo(path, out var family, out var canonicalPath))
        {
            await next();
            return;
        }

        var responseHeaders = context.HttpContext.Response.Headers;
        responseHeaders["Deprecation"] = "true";
        responseHeaders["Sunset"] = SunsetDateRfc1123;
        responseHeaders["Link"] = $"<{canonicalPath}>; rel=\"successor-version\"";
        responseHeaders["X-Regkasse-Canonical-Route"] = canonicalPath;

        var method = context.HttpContext.Request.Method;
        var routePattern = NormalizePathForMetric(path);
        _metrics.RecordLegacyRouteHit(family, routePattern, method);

        _logger.LogWarning(
            "Deprecated legacy API route used: family={LegacyFamily}, path={Path}, canonical={CanonicalPath}, method={Method}",
            family,
            path,
            canonicalPath,
            method);

        await next();
    }

    /// <summary>Reduces Prometheus label cardinality by replacing GUID path segments with {id}.</summary>
    public static string NormalizePathForMetric(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "unknown";
        return GuidSegment.Replace(path, "{id}");
    }

    public static bool TryGetLegacyInfo(string path, out string family, out string canonicalPath)
    {
        family = "";
        canonicalPath = "";

        if (path.StartsWith("/api/Payment", StringComparison.OrdinalIgnoreCase))
        {
            family = "payment";
            canonicalPath = "/api/pos/payment" + path.Substring("/api/Payment".Length);
            return true;
        }

        if (path.StartsWith("/api/Cart", StringComparison.OrdinalIgnoreCase))
        {
            family = "cart";
            canonicalPath = "/api/pos/cart" + path.Substring("/api/Cart".Length);
            return true;
        }

        if (path.StartsWith("/api/Product", StringComparison.OrdinalIgnoreCase))
        {
            family = "product";
            canonicalPath = "/api/pos" + path.Substring("/api/Product".Length);
            return true;
        }

        return false;
    }
}
