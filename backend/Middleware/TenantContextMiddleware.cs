using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Sets <see cref="ICurrentTenantAccessor"/> from the authenticated user's <c>tenant_id</c> JWT claim.
/// Development: when <see cref="SubdomainTenantProvider.DevTenantHeaderName"/> or <c>?tenant=</c> is present,
/// keeps the tenant already resolved by <see cref="TenantResolutionMiddleware"/> so local mandant switching works.
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

    public async Task InvokeAsync(HttpContext context, ICurrentTenantAccessor tenantAccessor)
    {
        if (_environment.IsDevelopment() && HasDevTenantOverride(context))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var raw = context.User?.FindFirst(ScopeCheckService.TenantIdClaim)?.Value;
        if (Guid.TryParse(raw, out var tenantId) && tenantId != Guid.Empty)
            tenantAccessor.TenantId = tenantId;

        await _next(context).ConfigureAwait(false);
    }

    public static bool HasDevTenantOverride(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(SubdomainTenantProvider.DevTenantHeaderName, out var headerTenant)
            && !string.IsNullOrWhiteSpace(headerTenant))
        {
            return true;
        }

        return context.Request.Query.TryGetValue(SubdomainTenantProvider.DevTenantQueryName, out var queryTenant)
               && !string.IsNullOrWhiteSpace(queryTenant);
    }
}
