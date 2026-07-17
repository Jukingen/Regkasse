using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// After authentication, re-resolves tenant via <see cref="ITenantContextService"/> (JWT-first).
/// Development: when <see cref="SubdomainTenantProvider.DevTenantHeaderName"/> / <c>?tenant=</c> is present,
/// re-binds tenant from the dev override via <see cref="ITenantContextService.ApplyFromRequestAsync"/> so it wins over JWT <c>tenant_id</c>.
/// </summary>
public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public TenantContextMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenantAccessor tenantAccessor,
        ITenantContextService tenantContextService)
    {
        // Development: dev header/query always wins over JWT tenant_id (local mandant switching).
        if (_environment.IsDevelopment() && HasDevTenantOverride(context))
        {
            await tenantContextService
                .ApplyFromRequestAsync(context, context.RequestAborted)
                .ConfigureAwait(false);
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var resolved = await tenantContextService
                .ResolveTenantContextAsync(context, context.RequestAborted)
                .ConfigureAwait(false);
            tenantAccessor.TenantId = resolved.Id;
        }

        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// True when Development mandant switching should win over JWT.
    /// Platform slug <c>admin</c> is not a mandant override (FA localhost / admin host).
    /// </summary>
    public static bool HasDevTenantOverride(HttpContext context)
    {
        if (!TryGetRawDevOverrideSlug(context, out var rawSlug))
        {
            return false;
        }

        return !IsPlatformAdminSlug(rawSlug);
    }

    private static bool TryGetRawDevOverrideSlug(HttpContext context, out string slug)
    {
        if (context.Request.Headers.TryGetValue(SubdomainTenantProvider.DevTenantHeaderName, out var headerTenant)
            && !string.IsNullOrWhiteSpace(headerTenant))
        {
            slug = headerTenant.ToString().Trim();
            return true;
        }

        if (context.Request.Query.TryGetValue(SubdomainTenantProvider.DevTenantQueryName, out var queryTenant)
            && !string.IsNullOrWhiteSpace(queryTenant))
        {
            slug = queryTenant.ToString().Trim();
            return true;
        }

        slug = string.Empty;
        return false;
    }

    private static bool IsPlatformAdminSlug(string slug) =>
        string.Equals(slug, "admin", StringComparison.OrdinalIgnoreCase);
}
