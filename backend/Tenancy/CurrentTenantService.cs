using KasseAPI_Final.Services.Tenancy;

namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Resolves the request tenant slug via <see cref="ITenantProvider"/> and sets <see cref="ICurrentTenantAccessor.TenantId"/>
/// for <see cref="AppDbContext"/> global query filters.
/// </summary>
public sealed class CurrentTenantService
{
    private readonly ITenantContextService _tenantContextService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(
        ITenantContextService tenantContextService,
        IHttpContextAccessor httpContextAccessor)
    {
        _tenantContextService = tenantContextService;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>Loads tenant by subdomain slug and updates the ambient accessor for this request scope.</summary>
    public Task ApplyCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available for tenant resolution");

        return _tenantContextService.ApplyFromRequestAsync(httpContext, cancellationToken);
    }
}
