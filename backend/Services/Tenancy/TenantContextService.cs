using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Services.Tenancy;

/// <summary>
/// Resolves tenant id/slug/name for the current HTTP request and binds <see cref="ICurrentTenantAccessor"/>.
/// Priority: JWT → dev header/query (development) → host slug → admin/default fallback.
/// </summary>
public sealed class TenantContextService : ITenantContextService
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TenantContextService> _logger;

    public TenantContextService(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        IWebHostEnvironment environment,
        ILogger<TenantContextService> logger)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _environment = environment;
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

        var requestSlug = GetHostTenantSlug(httpContext);
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
    public async Task ApplyFromRequestAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var slug = GetRequestTenantSlug(httpContext);
        _tenantAccessor.TenantId = await ResolveTenantIdFromSlugBindingAsync(slug, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ApplyFromHostAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var slug = GetHostTenantSlug(httpContext);
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

    private string GetRequestTenantSlug(HttpContext httpContext)
    {
        if (_environment.IsDevelopment())
        {
            var devSlug = GetDevTenantSlug(httpContext);
            if (!string.IsNullOrWhiteSpace(devSlug))
            {
                return devSlug;
            }
        }

        return GetHostTenantSlug(httpContext);
    }

    private static string GetHostTenantSlug(HttpContext httpContext) =>
        TenantHostNames.GetTenantSlugFromHost(httpContext.Request.Host.Host);

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
