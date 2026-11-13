using Microsoft.AspNetCore.Http;

namespace KasseAPI_Final.Services.PaymentGateway;

/// <summary>Default POS return URLs after hosted checkout / 3DS.</summary>
public static class PaymentReturnUrls
{
    public const string NativeDeepLink = "regkasse://payment-result";
    public const string WebPath = "/payment/result";

    public static string ResolveDefault(HttpContext? http)
    {
        if (IsWebClient(http))
            return ToAbsoluteWebReturnUrl(http);

        return NativeDeepLink;
    }

    public static bool IsWebClient(HttpContext? http)
    {
        var platform = http?.Request.Headers["X-App-Platform"].ToString() ?? string.Empty;
        if (platform.Equals("web", StringComparison.OrdinalIgnoreCase))
            return true;

        var ua = http?.Request.Headers.UserAgent.ToString() ?? string.Empty;
        return ua.Contains("Mozilla", StringComparison.OrdinalIgnoreCase)
            && !ua.Contains("ReactNative", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Turns <c>/payment/result</c> into an absolute URL when the request host is known.</summary>
    public static string ToAbsoluteIfRelative(string? returnUrl, HttpContext? http)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return ResolveDefault(http);

        var trimmed = returnUrl.Trim();
        if (!trimmed.StartsWith('/') || trimmed.StartsWith("//", StringComparison.Ordinal))
            return trimmed;

        if (http?.Request.Host.HasValue != true)
            return trimmed;

        var scheme = string.IsNullOrWhiteSpace(http.Request.Scheme) ? "https" : http.Request.Scheme;
        return $"{scheme}://{http.Request.Host.Value}{trimmed}";
    }

    private static string ToAbsoluteWebReturnUrl(HttpContext? http)
    {
        if (http?.Request.Host.HasValue == true)
        {
            var scheme = string.IsNullOrWhiteSpace(http.Request.Scheme) ? "https" : http.Request.Scheme;
            return $"{scheme}://{http.Request.Host.Value}{WebPath}";
        }

        return WebPath;
    }
}
