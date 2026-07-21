using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tenancy;

/// <summary>
/// Resolves tenant id/slug/name for the current HTTP request and binds <see cref="ICurrentTenantAccessor"/>.
/// Priority: JWT → (Development only) header/query → host slug → admin/default fallback.
/// Production authenticated binding uses <see cref="ApplyAuthenticatedTenantAsync"/> (JWT only).
/// </summary>
public sealed class TenantContextService : ITenantContextService
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IWebHostEnvironment _environment;
    private readonly ITenantDomainService _tenantDomains;
    private readonly ILogger<TenantContextService> _logger;

    public TenantContextService(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        IWebHostEnvironment environment,
        ITenantDomainService tenantDomains,
        ILogger<TenantContextService> logger)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _environment = environment;
        _tenantDomains = tenantDomains;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TenantContext> ResolveTenantContextAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var jwtTenantId = GetJwtTenantId(httpContext);
        if (jwtTenantId.HasValue)
        {
            var fromJwt = await TryResolveActiveTenantByIdAsync(jwtTenantId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (fromJwt != null)
            {
                return fromJwt;
            }

            // Claim present but inactive/missing — do not fall back to Host in Production (isolation).
            if (!_environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"JWT tenant_id '{jwtTenantId.Value:D}' could not be resolved to an active tenant");
            }
        }

        if (_environment.IsDevelopment())
        {
            var devSlug = GetDevTenantSlug(httpContext);
            if (!string.IsNullOrWhiteSpace(devSlug))
            {
                var fromDev = await TryResolveActiveTenantBySlugAsync(devSlug, cancellationToken)
                    .ConfigureAwait(false);
                if (fromDev != null)
                {
                    return fromDev;
                }
            }
        }

        var requestSlug = await GetHostTenantSlugAsync(httpContext, cancellationToken).ConfigureAwait(false);
        var fromRequest = await ResolveTenantContextFromSlugBindingAsync(requestSlug, cancellationToken)
            .ConfigureAwait(false);
        if (fromRequest != null)
        {
            return fromRequest;
        }

        if (IsAdminPlatformSlug(requestSlug))
        {
            var adminFallback = await TryResolveActiveTenantBySlugAsync("admin", cancellationToken)
                .ConfigureAwait(false);
            if (adminFallback != null)
            {
                return adminFallback;
            }
        }

        throw new InvalidOperationException("No tenant context could be resolved");
    }

    /// <inheritdoc />
    public async Task ApplyAuthenticatedTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        if (_environment.IsDevelopment())
        {
            var resolved = await ResolveTenantContextAsync(httpContext, cancellationToken)
                .ConfigureAwait(false);
            _tenantAccessor.TenantId = resolved.Id;
            return;
        }

        // Production / Staging: JWT tenant_id only (ignore Host and any X-Tenant-Id / ?tenant=).
        var jwtTenantId = GetJwtTenantId(httpContext);
        if (!jwtTenantId.HasValue)
        {
            _tenantAccessor.TenantId = null;
            return;
        }

        var fromJwt = await TryResolveActiveTenantByIdAsync(jwtTenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        _tenantAccessor.TenantId = fromJwt?.Id;
    }

    /// <inheritdoc />
    public async Task ApplyFromRequestAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var slug = await GetRequestTenantSlugAsync(httpContext, cancellationToken).ConfigureAwait(false);
        _tenantAccessor.TenantId = await ResolveTenantIdFromSlugBindingAsync(slug, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ApplyFromHostAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var slug = await GetHostTenantSlugAsync(httpContext, cancellationToken).ConfigureAwait(false);
        _tenantAccessor.TenantId = await ResolveTenantIdFromSlugBindingAsync(slug, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TenantContext?> ResolveTenantContextFromSlugBindingAsync(
        string rawSlug,
        CancellationToken cancellationToken)
    {
        var slug = NormalizeSlug(rawSlug);
        var tenantId = await ResolveTenantIdFromSlugBindingAsync(rawSlug, cancellationToken)
            .ConfigureAwait(false);
        if (!tenantId.HasValue)
        {
            return null;
        }

        var row = await _db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId.Value)
            .Select(t => new { t.Id, t.Slug, t.Name })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row == null ? null : new TenantContext(row.Id, row.Slug, row.Name);
    }

    private async Task<Guid?> ResolveTenantIdFromSlugBindingAsync(
        string rawSlug,
        CancellationToken cancellationToken)
    {
        var slug = NormalizeSlug(rawSlug);

        var tenant = await _db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Slug == slug)
            .Select(t => new { t.Id, t.Status, t.IsActive })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tenant == null)
        {
            _logger.LogWarning(
                "Tenant slug {Slug} not found; using legacy default tenant {DefaultTenantId}",
                slug,
                LegacyDefaultTenantIds.Primary);
            return LegacyDefaultTenantIds.Primary;
        }

        if (string.Equals(tenant.Status, TenantStatuses.Deleted, StringComparison.OrdinalIgnoreCase)
            || !tenant.IsActive)
        {
            _logger.LogWarning(
                "Tenant slug {Slug} is deleted or inactive (status={Status}); refusing host tenant binding",
                slug,
                tenant.Status);
            return null;
        }

        return tenant.Id;
    }

    private async Task<TenantContext?> TryResolveActiveTenantBySlugAsync(
        string rawSlug,
        CancellationToken cancellationToken)
    {
        var slug = NormalizeSlug(rawSlug);
        var row = await _db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Slug == slug)
            .Select(t => new { t.Id, t.Slug, t.Name, t.Status, t.IsActive })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row == null
            || string.Equals(row.Status, TenantStatuses.Deleted, StringComparison.OrdinalIgnoreCase)
            || !row.IsActive)
        {
            return null;
        }

        return new TenantContext(row.Id, row.Slug, row.Name);
    }

    private async Task<TenantContext?> TryResolveActiveTenantByIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var row = await _db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Id, t.Slug, t.Name, t.Status, t.IsActive })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row == null
            || string.Equals(row.Status, TenantStatuses.Deleted, StringComparison.OrdinalIgnoreCase)
            || !row.IsActive)
        {
            return null;
        }

        return new TenantContext(row.Id, row.Slug, row.Name);
    }

    private async Task<string> GetRequestTenantSlugAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (_environment.IsDevelopment())
        {
            var devSlug = GetDevTenantSlug(httpContext);
            if (!string.IsNullOrWhiteSpace(devSlug))
            {
                return devSlug;
            }
        }

        return await GetHostTenantSlugAsync(httpContext, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetHostTenantSlugAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var host = httpContext.Request.Host.Host;
        var customSlug = await _tenantDomains.TryResolveSlugByHostAsync(host, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(customSlug))
            return customSlug;

        return TenantHostNames.GetTenantSlugFromHost(host);
    }

    private static bool IsAdminPlatformSlug(string slug) =>
        string.Equals(slug, "admin", StringComparison.OrdinalIgnoreCase);

    private static string? GetDevTenantSlug(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(SubdomainTenantProvider.DevTenantHeaderName, out var headerTenant)
            && !string.IsNullOrWhiteSpace(headerTenant))
        {
            return headerTenant.ToString().Trim();
        }

        if (httpContext.Request.Query.TryGetValue(SubdomainTenantProvider.DevTenantQueryName, out var queryTenant)
            && !string.IsNullOrWhiteSpace(queryTenant))
        {
            return queryTenant.ToString().Trim();
        }

        return null;
    }

    private static Guid? GetJwtTenantId(HttpContext httpContext)
    {
        var raw = httpContext.User?.FindFirst(ScopeCheckService.TenantIdClaim)?.Value;
        return Guid.TryParse(raw, out var tenantId) && tenantId != Guid.Empty ? tenantId : null;
    }

    private static string NormalizeSlug(string slug)
    {
        if (string.Equals(slug, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return LegacyDefaultTenantIds.PrimarySlug;
        }

        return DevTenantSlugAliases.ResolveCanonical(slug);
    }
}
