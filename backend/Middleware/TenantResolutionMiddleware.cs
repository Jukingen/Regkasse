using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Resolves tenant from the request before auth and sets <see cref="ICurrentTenantAccessor"/>.
/// <para>
/// <b>Tenant resolution order</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Development:</b>
/// <c>X-Tenant-Id</c> header → <c>?tenant=</c> query → Host slug → DX default (<c>dev</c> when Host is loopback/<c>admin</c>).
/// Prefer header over query (REST-friendly, less cacheable).
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Production/Staging (shared platform hosts <c>api</c>/<c>pos</c>/<c>admin</c>/<c>www</c> / loopback):</b>
/// leave ambient unset here; JWT <c>tenant_id</c> binds later via <see cref="TenantContextMiddleware"/>.
/// Header and query overrides are ignored (query presence is logged as Warning for ops alerting).
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Production/Staging (mandant subdomain / custom domain):</b>
/// Host slug binds ambient tenant for public site APIs; authenticated traffic still rebinds from JWT.
/// </description>
/// </item>
/// </list>
/// Pre-auth host/dev header → Guid on <see cref="ICurrentTenantAccessor"/>.
/// Unknown slugs leave ambient unset (TenantValidationMiddleware → HTTP 404 on tenant-scoped paths).
/// Pipeline: runs after <see cref="CsrfMiddleware"/> and before authentication.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _environment = environment;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        CurrentTenantService currentTenantService,
        ICurrentTenantAccessor tenantAccessor)
    {
        // Monitor accidental ?tenant= usage (Dev: Debug when query is the effective source; Prod: Warning / alert).
        LogQueryParameterUsage(context);

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

    /// <summary>
    /// Tracks <c>?tenant=</c> presence so Production misuse can be alerted via Warning logs.
    /// Development logs Debug only when the query is the effective override (no header).
    /// </summary>
    private void LogQueryParameterUsage(HttpContext context)
    {
        if (!context.Request.Query.TryGetValue(SubdomainTenantProvider.DevTenantQueryName, out var queryTenant)
            || string.IsNullOrWhiteSpace(queryTenant))
        {
            return;
        }

        var tenantFromQuery = queryTenant.ToString().Trim();

        if (_environment.IsDevelopment())
        {
            var hasHeader = context.Request.Headers.TryGetValue(
                    SubdomainTenantProvider.DevTenantHeaderName,
                    out var headerTenant)
                && !string.IsNullOrWhiteSpace(headerTenant);

            // Header wins over query; only treat query as the resolution source when header is absent.
            if (!hasHeader)
            {
                _logger.LogDebug(
                    "Development: Tenant resolved from query parameter: {Tenant}",
                    tenantFromQuery);
            }
        }
        else
        {
            _logger.LogWarning(
                "Production: Query parameter ignored for tenant resolution: {Tenant}",
                tenantFromQuery);
        }
    }
}
