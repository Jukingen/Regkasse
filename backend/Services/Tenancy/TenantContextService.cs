using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tenancy;

/// <summary>
/// Resolves tenant id/slug/name for the current HTTP request and binds <see cref="ICurrentTenantAccessor"/>.
/// Priority: JWT → (Development only) header/query → (Development SuperAdmin) seeded <c>dev</c> → host slug → admin fallback.
/// Production authenticated binding uses <see cref="ApplyAuthenticatedTenantAsync"/> (JWT only; never silent SuperAdmin defaults).
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
            if (!string.IsNullOrWhiteSpace(devSlug) && !IsAdminPlatformSlug(devSlug))
            {
                var fromDev = await TryResolveActiveTenantBySlugAsync(devSlug, cancellationToken)
                    .ConfigureAwait(false);
                if (fromDev != null)
                {
                    return fromDev;
                }
            }

            // Super Admin on FA localhost without JWT/header: prefer seeded `dev`.
            if (!jwtTenantId.HasValue && IsSuperAdmin(httpContext))
            {
                var superAdminDefault = await TryResolveSuperAdminDevelopmentDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (superAdminDefault != null)
                {
                    return superAdminDefault;
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
            BindAmbient(resolved.Id, resolved.Slug);
            return;
        }

        // Production / Staging: JWT tenant_id only (ignore Host and any X-Tenant-Id / ?tenant=).
        // SuperAdmin is not given a silent default — FA must rebind JWT (refresh/impersonation).
        var jwtTenantId = GetJwtTenantId(httpContext);
        if (!jwtTenantId.HasValue)
        {
            ClearAmbient();
            return;
        }

        var fromJwt = await TryResolveActiveTenantByIdAsync(jwtTenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        BindAmbient(fromJwt?.Id, fromJwt?.Slug);
    }

    /// <inheritdoc />
    public async Task ApplyFromRequestAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var slug = await GetRequestTenantSlugAsync(httpContext, cancellationToken).ConfigureAwait(false);
        var tenantId = await ResolveTenantIdFromSlugBindingAsync(slug, cancellationToken)
            .ConfigureAwait(false);
        BindAmbient(tenantId, tenantId.HasValue ? NormalizeSlug(slug) : null);
    }

    /// <inheritdoc />
    public async Task ApplyFromHostAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var slug = await GetHostTenantSlugAsync(httpContext, cancellationToken).ConfigureAwait(false);
        var tenantId = await ResolveTenantIdFromSlugBindingAsync(slug, cancellationToken)
            .ConfigureAwait(false);
        BindAmbient(tenantId, tenantId.HasValue ? NormalizeSlug(slug) : null);
    }

    /// <inheritdoc />
    public async Task<Guid?> TryResolveHostBoundTenantIdAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var slug = await GetHostTenantSlugAsync(httpContext, cancellationToken).ConfigureAwait(false);
        return await ResolveTenantIdFromSlugBindingAsync(slug, cancellationToken).ConfigureAwait(false);
    }

    private void BindAmbient(Guid? tenantId, string? tenantSlug)
    {
        _tenantAccessor.TenantId = tenantId;
        _tenantAccessor.TenantSlug = tenantId.HasValue ? tenantSlug : null;
    }

    private void ClearAmbient()
    {
        _tenantAccessor.TenantId = null;
        _tenantAccessor.TenantSlug = null;
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
            // Fail-closed for unknown / typo mandant slugs (do not invent platform ambient).
            // Reserved platform sentinel slug may still bind by well-known Guid when the row is missing
            // (admin host → NormalizeSlug → "platform").
            if (SystemTenantIds.IsPlatformSlug(slug))
            {
                _logger.LogWarning(
                    "Platform slug {Slug} row missing; binding well-known platform tenant {PlatformTenantId}",
                    slug,
                    SystemTenantIds.Platform);
                return SystemTenantIds.Platform;
            }

            _logger.LogWarning("Tenant slug {Slug} not found; refusing host tenant binding", slug);
            return null;
        }

        // Platform sentinel may be isActive=false (frozen for business) but remains bindable for system host fallbacks.
        if (SystemTenantIds.IsPlatformTenantId(tenant.Id))
        {
            if (TenantStatuses.IsRemoved(tenant.Status))
            {
                _logger.LogWarning(
                    "Platform tenant slug {Slug} is removed (status={Status}); refusing host tenant binding",
                    slug,
                    tenant.Status);
                return null;
            }

            return tenant.Id;
        }

        if (TenantStatuses.IsRemoved(tenant.Status)
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
            || TenantStatuses.IsRemoved(row.Status))
        {
            return null;
        }

        if (!row.IsActive && !SystemTenantIds.IsPlatformTenantId(row.Id))
            return null;

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
            || TenantStatuses.IsRemoved(row.Status))
        {
            return null;
        }

        if (!row.IsActive && !SystemTenantIds.IsPlatformTenantId(row.Id))
            return null;

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

    /// <summary>
    /// Development Super Admin without JWT / mandant header: seeded <c>dev</c> preset (not legacy <c>default</c>).
    /// </summary>
    private async Task<TenantContext?> TryResolveSuperAdminDevelopmentDefaultAsync(
        CancellationToken cancellationToken)
    {
        var fromSlug = await TryResolveActiveTenantBySlugAsync("dev", cancellationToken)
            .ConfigureAwait(false);
        if (fromSlug != null)
        {
            _logger.LogDebug(
                "Bound Development SuperAdmin ambient tenant to seeded preset {TenantId} ({Slug})",
                fromSlug.Id,
                fromSlug.Slug);
            return fromSlug;
        }

        // Stable id fallback when slug row missing but seed id exists.
        return await TryResolveActiveTenantByIdAsync(DemoTenantIds.Dev, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsSuperAdmin(HttpContext httpContext)
    {
        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole(Roles.SuperAdmin))
        {
            return true;
        }

        // Some tokens emit a custom "role" claim instead of ClaimTypes.Role.
        foreach (var claim in user.FindAll("role"))
        {
            if (string.Equals(claim.Value, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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

    /// <summary>
    /// Development: platform <c>admin</c> binds to seeded <c>dev</c>; legacy demo aliases (<c>cafe</c>→<c>dev</c>) apply.
    /// Production/Staging: <c>admin</c> maps to the platform sentinel slug for reserved-host binding only.
    /// Demo aliases are Development-only — Production resolves the exact slug (unknown → null / 404).
    /// Explicit <c>default</c> is not aliased (legacy slug removed).
    /// </summary>
    private string NormalizeSlug(string slug)
    {
        if (string.Equals(slug, "admin", StringComparison.OrdinalIgnoreCase))
        {
            if (_environment.IsDevelopment())
            {
                // In development, we always use 'dev' tenant. Tenant switcher / X-Tenant-Id can still override.
                return "dev";
            }

            return SystemTenantIds.PlatformSlug;
        }

        if (_environment.IsDevelopment())
            return DevTenantSlugAliases.ResolveCanonical(slug);

        return string.IsNullOrWhiteSpace(slug) ? slug : slug.Trim();
    }
}
