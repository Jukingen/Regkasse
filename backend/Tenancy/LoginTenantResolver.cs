using KasseAPI_Final.Data;
using KasseAPI_Final.Localization;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Tenancy;

/// <inheritdoc />
public sealed class LoginTenantResolver : ILoginTenantResolver
{
    private readonly AppDbContext _db;
    private readonly ILogger<LoginTenantResolver> _logger;
    private readonly ITenantProvider _tenantProvider;
    private readonly IWebHostEnvironment _environment;

    public LoginTenantResolver(
        AppDbContext db,
        ILogger<LoginTenantResolver> logger,
        ITenantProvider tenantProvider,
        IWebHostEnvironment environment)
    {
        _db = db;
        _logger = logger;
        _tenantProvider = tenantProvider;
        _environment = environment;
    }

    /// <inheritdoc />
    public async Task<AuthTenantSnapshot> ResolveSnapshotForLoginAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Memberships are ITenantEntity; login must see all tenants for the user (fail-closed filters hide others).
        var active = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(m => m.Tenant)
            .Where(m => m.UserId == userId
                && m.IsActive
                && m.Tenant != null
                && m.Tenant.Status != TenantStatuses.Deleted
                && m.Tenant.IsActive)
            .OrderBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (_environment.IsDevelopment() && active.Count > 0)
        {
            var requestSlug = NormalizeRequestSlug(_tenantProvider.GetCurrentTenantId());
            if (requestSlug != null)
            {
                var matched = active.FirstOrDefault(m =>
                    string.Equals(m.Tenant?.Slug, requestSlug, StringComparison.OrdinalIgnoreCase));
                if (matched != null)
                {
                    return await BuildSnapshotFromMembershipAsync(matched, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        if (active.Count == 0)
        {
            if (await HasDeletedTenantMembershipOnlyAsync(userId, cancellationToken).ConfigureAwait(false))
            {
                throw new LoginTenantBlockedException(
                    ApiMessageCatalog.Get(ApiMessageKeys.TenantDisabled, LanguageMiddleware.DefaultLanguage),
                    LoginTenantBlockedException.CodeTenantDisabled);
            }

            _logger.LogWarning(
                "Login tenant: user {UserId} has no active membership; using legacy default tenant (configure membership provisioning or enable RequireTenantMembershipForLogin to block).",
                userId);
            return await ResolveLegacyDefaultSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }

        if (active.Count > 1)
        {
            var preferred = PickPreferredMembership(active);
            _logger.LogCritical(
                "Login tenant: user {UserId} has {Count} active memberships; expected at most one. Using preferred tenant {TenantId} ({TenantSlug}). Fix data or add tenant switch before enabling multi-tenant.",
                userId,
                active.Count,
                preferred.TenantId,
                preferred.Tenant?.Slug);
            return await BuildSnapshotFromMembershipAsync(preferred, cancellationToken).ConfigureAwait(false);
        }

        return await BuildSnapshotFromMembershipAsync(active[0], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Multi-membership tie-break when request slug is missing/admin:
    /// prefer demo <c>dev</c> (POS local default), else oldest non-legacy-default, else oldest.
    /// </summary>
    internal static UserTenantMembership PickPreferredMembership(IReadOnlyList<UserTenantMembership> active)
    {
        if (active.Count == 0)
        {
            throw new ArgumentException("Expected at least one membership.", nameof(active));
        }

        var dev = active.FirstOrDefault(m =>
            m.TenantId == DemoTenantIds.Dev
            || string.Equals(m.Tenant?.Slug, "dev", StringComparison.OrdinalIgnoreCase));
        if (dev != null)
        {
            return dev;
        }

        var nonLegacy = active.FirstOrDefault(m => m.TenantId != LegacyDefaultTenantIds.Primary);
        if (nonLegacy != null)
        {
            return nonLegacy;
        }

        return active[0];
    }

    private static string? NormalizeRequestSlug(string? slug)
    {
        var trimmed = slug?.Trim();
        if (string.IsNullOrEmpty(trimmed)
            || string.Equals(trimmed, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }

    private async Task<AuthTenantSnapshot> BuildSnapshotFromMembershipAsync(
        UserTenantMembership chosen,
        CancellationToken cancellationToken)
    {
        var name = chosen.Tenant?.Name;
        var slug = chosen.Tenant?.Slug;
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(slug))
        {
            var row = await _db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == chosen.TenantId, cancellationToken)
                .ConfigureAwait(false);
            name ??= row?.Name;
            slug ??= row?.Slug;
        }

        return new AuthTenantSnapshot(chosen.TenantId.ToString("D"), name, slug, null, null);
    }

    /// <inheritdoc />
    public Task<bool> HasActiveMembershipAsync(string userId, CancellationToken cancellationToken = default) =>
        _db.UserTenantMemberships.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive)
            .Join(
                _db.Tenants.AsNoTracking(),
                m => m.TenantId,
                t => t.Id,
                (_, t) => t)
            .AnyAsync(
                t => t.Status != TenantStatuses.Deleted && t.IsActive,
                cancellationToken);

    private async Task<bool> HasDeletedTenantMembershipOnlyAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new
            {
                m.IsActive,
                TenantStatus = m.Tenant!.Status,
                TenantIsActive = m.Tenant!.IsActive,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
            return false;

        var hasEligible = rows.Any(m =>
            m.IsActive
            && m.TenantStatus != TenantStatuses.Deleted
            && m.TenantIsActive);

        return !hasEligible
            && rows.Any(m => m.TenantStatus == TenantStatuses.Deleted);
    }

    private async Task<AuthTenantSnapshot> ResolveLegacyDefaultSnapshotAsync(CancellationToken cancellationToken)
    {
        var primary = LegacyDefaultTenantIds.Primary;
        var rowDefault = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == primary, cancellationToken)
            .ConfigureAwait(false);

        return new AuthTenantSnapshot(primary.ToString("D"), rowDefault?.Name, rowDefault?.Slug, null, null);
    }
}
