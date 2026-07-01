using System.Text.Json;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Middleware;

public class TenantValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantValidationMiddleware> _logger;

    // Public endpoints that don't require tenant context
    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/health",
        "/swagger",
        "/swagger/index.html",
    };

    // Super Admin platform endpoints that work without mandant tenant context (admin.regkasse.at).
    private static readonly HashSet<string> SuperAdminPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/admin/tenants",
        "/api/admin/tenants/switcher",
        "/api/admin/billing",
    };

    public TenantValidationMiddleware(RequestDelegate next, ILogger<TenantValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTenantAccessor tenantAccessor)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        var isPublicEndpoint = PublicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        var isSuperAdminEndpoint = SuperAdminPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        // Super Admin endpoints work without tenant
        if (isSuperAdminEndpoint)
        {
            await _next(context);
            return;
        }

        // Public endpoints work without tenant
        if (isPublicEndpoint)
        {
            await _next(context);
            return;
        }

        // All other endpoints require tenant context
        if (tenantAccessor.TenantId == null)
        {
            _logger.LogWarning("Request to {Path} rejected: No tenant context", path);

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";

            var error = new
            {
                error = "Not Found",
                message = "The requested resource could not be found",
                status = 404,
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(error));
            return;
        }

        await _next(context);
    }
}
