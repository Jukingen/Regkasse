using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Resolves tenant from the request before auth and sets <see cref="ICurrentTenantAccessor"/>.
/// <list type="bullet">
/// <item><description>Development: <see cref="SubdomainTenantProvider.DevTenantHeaderName"/> / <c>?tenant=</c> wins over host slug.</description></item>
/// <item><description>Production/Staging: shared platform hosts (<c>api</c>/<c>pos</c>/<c>admin</c>/<c>www</c>) and loopback leave ambient tenant unset — JWT <c>tenant_id</c> binds later via <see cref="TenantContextMiddleware"/>.</description></item>
/// <item><description>Production/Staging: custom domains / mandant subdomains still bind from Host for public site APIs.</description></item>
/// </list>
/// Pipeline: runs after <see cref="CsrfMiddleware"/> and before authentication.
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

    public async Task InvokeAsync(
        HttpContext context,
        CurrentTenantService currentTenantService,
        ICurrentTenantAccessor tenantAccessor)
    {
        if (_environment.IsDevelopment() && TenantContextMiddleware.HasDevTenantOverride(context))
        {
            await currentTenantService
                .ApplyDevTenantOverrideAsync(context.RequestAborted)
                .ConfigureAwait(false);
        }
        else if (!_environment.IsDevelopment()
                 && TenantHostNames.ShouldSkipPreAuthHostBinding(context.Request.Host.Host))
        {
            // Platform hosts: do not inherit a stale ambient tenant from a previous misuse of the accessor.
            tenantAccessor.TenantId = null;
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
