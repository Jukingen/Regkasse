using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Resolves the request tenant slug via <see cref="ITenantProvider"/> and sets <see cref="ICurrentTenantAccessor.TenantId"/>
/// for <see cref="AppDbContext"/> global query filters.
/// </summary>
public sealed class CurrentTenantService
{
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly AppDbContext _db;
    private readonly ILogger<CurrentTenantService> _logger;

    public CurrentTenantService(
        ITenantProvider tenantProvider,
        ICurrentTenantAccessor tenantAccessor,
        AppDbContext db,
        ILogger<CurrentTenantService> logger)
    {
        _tenantProvider = tenantProvider;
        _tenantAccessor = tenantAccessor;
        _db = db;
        _logger = logger;
    }

    /// <summary>Loads tenant by subdomain slug and updates the ambient accessor for this request scope.</summary>
    public async Task ApplyCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        var slug = NormalizeSlug(_tenantProvider.GetCurrentTenantId());

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
            _tenantAccessor.TenantId = LegacyDefaultTenantIds.Primary;
            return;
        }

        if (string.Equals(tenant.Status, TenantStatuses.Deleted, StringComparison.OrdinalIgnoreCase)
            || !tenant.IsActive)
        {
            _logger.LogWarning(
                "Tenant slug {Slug} is deleted or inactive (status={Status}); refusing host tenant binding",
                slug,
                tenant.Status);
            _tenantAccessor.TenantId = null;
            return;
        }

        _tenantAccessor.TenantId = tenant.Id;
    }

    private static string NormalizeSlug(string slug) =>
        string.Equals(slug, "admin", StringComparison.OrdinalIgnoreCase)
            ? LegacyDefaultTenantIds.PrimarySlug
            : slug.Trim();
}
