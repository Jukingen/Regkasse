using KasseAPI_Final.Services.Tenancy;

namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Resolves the request tenant slug via <see cref="ITenantProvider"/> / <see cref="ITenantContextService"/>
/// and sets <see cref="ICurrentTenantAccessor.TenantId"/> for <see cref="Data.AppDbContext"/> global query filters.
/// </summary>
public sealed class CurrentTenantService
{
    private readonly ITenantContextService _tenantContextService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;

    public CurrentTenantService(
        ITenantContextService tenantContextService,
        IHttpContextAccessor httpContextAccessor,
        IWebHostEnvironment environment)
    {
        _tenantContextService = tenantContextService;
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
    }

    /// <summary>
    /// Binds ambient tenant from the current request (Development may use header/query; otherwise host).
    /// Prefer middleware entry points (<see cref="ApplyDevTenantOverrideAsync"/> / <see cref="ApplyFromHostAsync"/>).
    /// </summary>
    public Task ApplyCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = RequireHttpContext();
        return _tenantContextService.ApplyFromRequestAsync(httpContext, cancellationToken);
    }

    /// <summary>
    /// Development only: binds tenant from <c>X-Tenant-Id</c> / <c>?tenant=</c> slug override.
    /// Throws when called outside Development.
    /// </summary>
    public Task ApplyDevTenantOverrideAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "X-Tenant-Id / ?tenant= overrides are only allowed in Development.");
        }

        var httpContext = RequireHttpContext();
        return _tenantContextService.ApplyFromRequestAsync(httpContext, cancellationToken);
    }

    /// <summary>Pre-auth binding from request host / custom domain only (ignores header/query).</summary>
    public Task ApplyFromHostAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = RequireHttpContext();
        return _tenantContextService.ApplyFromHostAsync(httpContext, cancellationToken);
    }

    private HttpContext RequireHttpContext() =>
        _httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("HttpContext is not available for tenant resolution");
}
