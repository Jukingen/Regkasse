using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// After authentication, re-resolves tenant via <see cref="ITenantContextService"/> (JWT-first).
/// Unauthenticated requests keep the slug binding from <see cref="TenantResolutionMiddleware"/>.
/// </summary>
public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenantAccessor tenantAccessor,
        ITenantContextService tenantContextService)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var resolved = await tenantContextService
                .ResolveTenantContextAsync(context, context.RequestAborted)
                .ConfigureAwait(false);
            tenantAccessor.TenantId = resolved.Id;
        }

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
