namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Maps request host subdomain to a tenant slug (e.g. <c>companyA.regkasse.at</c> → <c>companyA</c>).
/// Development: override via <see cref="DevTenantHeaderName"/> or <c>?tenant=slug</c>.
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
