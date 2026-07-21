using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// After authentication, re-binds ambient tenant.
/// <list type="bullet">
/// <item><description>Development: when <see cref="SubdomainTenantProvider.DevTenantHeaderName"/> / <c>?tenant=</c> is present (and not platform <c>admin</c>), that override wins over JWT.</description></item>
/// <item><description>Production/Staging: authenticated requests use JWT <c>tenant_id</c> only — header/query are ignored; missing/invalid claim clears ambient tenant (fail-closed).</description></item>
/// </list>
/// Pipeline: runs immediately after <c>UseAuthentication</c> and before license / authorization gates.
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
            await tenantContextService
                .ApplyAuthenticatedTenantAsync(context, context.RequestAborted)
                .ConfigureAwait(false);
        }

        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// True when Development mandant switching should win over JWT.
    /// Platform slug <c>admin</c> is not a mandant override (FA localhost / admin host).
    /// Callers must also gate on Development — this helper does not check environment.
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
