using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Tenancy;

/// <inheritdoc />
public sealed class LoginTenantResolver : ILoginTenantResolver
{
    private readonly AppDbContext _db;
    private readonly ILogger<LoginTenantResolver> _logger;

    public LoginTenantResolver(AppDbContext db, ILogger<LoginTenantResolver> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AuthTenantSnapshot> ResolveSnapshotForLoginAsync(string userId, CancellationToken cancellationToken = default)
    {
        var active = await _db.UserTenantMemberships
            .AsNoTracking()
            .Include(m => m.Tenant)
            .Where(m => m.UserId == userId && m.IsActive)
            .OrderBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (active.Count == 0)
        {
            _logger.LogWarning(
                "Login tenant: user {UserId} has no active membership; using legacy default tenant (configure membership provisioning or enable RequireTenantMembershipForLogin to block).",
                userId);
            return await ResolveLegacyDefaultSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }

        if (active.Count > 1)
        {
            _logger.LogCritical(
                "Login tenant: user {UserId} has {Count} active memberships; expected at most one. Using oldest by CreatedAtUtc. Fix data or add tenant switch before enabling multi-tenant.",
                userId,
                active.Count);
        }

        var chosen = active[0];
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
        _db.UserTenantMemberships.AsNoTracking()
            .AnyAsync(m => m.UserId == userId && m.IsActive, cancellationToken);

    private async Task<AuthTenantSnapshot> ResolveLegacyDefaultSnapshotAsync(CancellationToken cancellationToken)
    {
        var primary = LegacyDefaultTenantIds.Primary;
        var rowDefault = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == primary, cancellationToken)
            .ConfigureAwait(false);

        return new AuthTenantSnapshot(primary.ToString("D"), rowDefault?.Name, rowDefault?.Slug, null, null);
    }
}
