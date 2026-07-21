namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Maps request host subdomain to a tenant slug (e.g. <c>companyA.regkasse.at</c> → <c>companyA</c>).
/// <see cref="DevTenantHeaderName"/> / <see cref="DevTenantQueryName"/> are read only when
/// <see cref="IHostEnvironment.IsDevelopment"/> — ignored in Production/Staging.
/// </summary>
public sealed class SubdomainTenantProvider : ITenantProvider
{
    public const string DevTenantHeaderName = "X-Tenant-Id";
    public const string DevTenantQueryName = "tenant";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;

    public SubdomainTenantProvider(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment environment)
    {
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
    }

    /// <inheritdoc />
    public string GetCurrentTenantId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        // Fail-closed: never honor client-supplied tenant overrides outside Development.
        if (_environment.IsDevelopment() && httpContext != null)
        {
            if (httpContext.Request.Headers.TryGetValue(DevTenantHeaderName, out var headerTenant)
                && !string.IsNullOrWhiteSpace(headerTenant))
            {
                return headerTenant.ToString().Trim();
            }

            if (httpContext.Request.Query.TryGetValue(DevTenantQueryName, out var queryTenant)
                && !string.IsNullOrWhiteSpace(queryTenant))
            {
                return queryTenant.ToString().Trim();
            }
        }

        return TenantHostNames.GetTenantSlugFromHost(httpContext?.Request.Host.Host);
    }
}
