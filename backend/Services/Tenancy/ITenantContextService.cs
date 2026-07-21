namespace KasseAPI_Final.Services.Tenancy;

/// <summary>
/// Resolves the effective mandant for the current HTTP request (host, dev header, JWT).
/// </summary>
public interface ITenantContextService
{
    /// <summary>
    /// Full resolution: JWT → (Development only) header/query → host slug → admin/default fallback.
    /// </summary>
    Task<TenantContext> ResolveTenantContextAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// After authentication: Development uses full resolution; Production/Staging binds JWT <c>tenant_id</c> only
    /// (clears ambient tenant when the claim is missing or inactive).
    /// </summary>
    Task ApplyAuthenticatedTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pre-auth binding: request slug → <see cref="Tenancy.ICurrentTenantAccessor"/> (matches host/header dev switcher).
    /// Development may use <c>X-Tenant-Id</c> / <c>?tenant=</c>; Production uses host only.
    /// </summary>
    Task ApplyFromRequestAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pre-auth binding from request host subdomain / custom domain only (ignores header/query).
    /// </summary>
    Task ApplyFromHostAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default);
}
