namespace KasseAPI_Final.Services.Tenancy;

/// <summary>
/// Resolves the effective mandant for the current HTTP request (host, dev header, JWT).
/// </summary>
public interface ITenantContextService
{
    /// <summary>
    /// Full resolution: JWT → dev header/query (development) → host slug → admin/default fallback.
    /// </summary>
    Task<TenantContext> ResolveTenantContextAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pre-auth binding: request slug → <see cref="Tenancy.ICurrentTenantAccessor"/> (matches host/header dev switcher).
    /// </summary>
    Task ApplyFromRequestAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pre-auth binding from request host subdomain only (ignores dev header/query).
    /// </summary>
    Task ApplyFromHostAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default);
}
