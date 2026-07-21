using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Security;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Double-submit CSRF for state-changing requests (cookie <c>XSRF-TOKEN</c> + header <c>X-XSRF-TOKEN</c>).
/// Skips safe methods, public auth/health paths, webhooks, and Development when bypass is enabled.
/// </summary>
public class CsrfMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CsrfMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<CsrfOptions> _options;

    public CsrfMiddleware(
        RequestDelegate next,
        ILogger<CsrfMiddleware> logger,
        IHostEnvironment environment,
        IOptionsMonitor<CsrfOptions> options)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context, ICsrfTokenService csrfService)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            await _next(context);
            return;
        }

        // Development bypass (only honored when ASPNETCORE_ENVIRONMENT=Development).
        if (_environment.IsDevelopment() && options.BypassInDevelopment)
        {
            await _next(context);
            return;
        }

        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;

        // Safe methods + public endpoints (login, refresh, health, swagger, webhooks, token issue).
        if (HttpMethods.IsGet(method) ||
            HttpMethods.IsHead(method) ||
            HttpMethods.IsOptions(method) ||
            IsExemptPath(path))
        {
            await _next(context);
            return;
        }

        var headerName = string.IsNullOrWhiteSpace(options.HeaderName)
            ? "X-XSRF-TOKEN"
            : options.HeaderName.Trim();
        var cookieName = string.IsNullOrWhiteSpace(options.CookieName)
            ? "XSRF-TOKEN"
            : options.CookieName.Trim();

        var cookieToken = context.Request.Cookies[cookieName]
            ?? context.Request.Headers["X-CSRF-COOKIE"].FirstOrDefault()
            ?? string.Empty;
        var headerToken = context.Request.Headers[headerName].FirstOrDefault() ?? string.Empty;

        if (!csrfService.ValidateToken(headerToken, cookieToken))
        {
            _logger.LogWarning(
                "CSRF validation failed for {Path} from {IP}",
                path,
                context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "Invalid CSRF token. Please refresh the page and try again."
            });
            return;
        }

        await _next(context);
    }

    internal static bool IsExemptPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth/refresh", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth/verify-2fa", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/webhooks", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/webhooks/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/csrf/token", StringComparison.OrdinalIgnoreCase);
    }
}
