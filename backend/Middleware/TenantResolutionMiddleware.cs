using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Resolves tenant from the request before auth and sets <see cref="ICurrentTenantAccessor"/>.
/// Development: <see cref="SubdomainTenantProvider.DevTenantHeaderName"/> / <c>?tenant=</c> wins over host slug.
/// JWT <see cref="TenantContextMiddleware"/> may re-bind after authentication (dev override still wins in Development).
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public TenantResolutionMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context, CurrentTenantService currentTenantService)
    {
        if (_environment.IsDevelopment() && TenantContextMiddleware.HasDevTenantOverride(context))
        {
            await currentTenantService
                .ApplyDevTenantOverrideAsync(context.RequestAborted)
                .ConfigureAwait(false);
        }
        else
        {
            await currentTenantService
                .ApplyFromHostAsync(context.RequestAborted)
                .ConfigureAwait(false);
        }

        await _next(context).ConfigureAwait(false);
    }
}
