using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Resolves tenant from the request host (subdomain) before auth and sets <see cref="ICurrentTenantAccessor"/>.
/// JWT <see cref="TenantContextMiddleware"/> re-resolves via <see cref="Services.Tenancy.ITenantContextService"/> after authentication.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, CurrentTenantService currentTenantService)
    {
        await currentTenantService.ApplyCurrentTenantAsync(context.RequestAborted).ConfigureAwait(false);
        await _next(context).ConfigureAwait(false);
    }
}
