using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Data;

/// <summary>
/// Idempotent data heal: demo/POS users must not keep an active legacy <c>default</c> membership
/// alongside an active demo mandant (<c>dev</c>/<c>prod</c>). Dual rows make FA login resolve the wrong JWT tenant.
/// </summary>
public static class UserTenantMembershipHealSeed
{
    /// <summary>
    /// Development only: deactivate legacy-default memberships when the same user also has an active
    /// demo-tenant membership (<see cref="DemoTenantIds"/>).
    /// </summary>
    public static async Task HealLegacyDefaultAlongsideDemoTenantsAsync(
        AppDbContext db,
        IHostEnvironment hostEnvironment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!hostEnvironment.IsDevelopment())
            return;

        var healed = await HealLegacyDefaultAlongsideDemoTenantsCoreAsync(db, cancellationToken)
            .ConfigureAwait(false);
        if (healed > 0)
        {
            logger.LogWarning(
                "Healed {Count} active legacy-default membership(s) for users who also belong to demo tenants (dev/prod).",
                healed);
        }
    }

    /// <summary>Testable core (no environment gate).</summary>
    internal static async Task<int> HealLegacyDefaultAlongsideDemoTenantsCoreAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        var demoTenantIds = DemoTenantIds.All.ToHashSet();
        var legacyId = LegacyDefaultTenantIds.Primary;

        var userIdsWithDemo = await db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.IsActive && demoTenantIds.Contains(m.TenantId))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (userIdsWithDemo.Count == 0)
            return 0;

        var legacyRows = await db.UserTenantMemberships
            .IgnoreQueryFilters()
            .Where(m =>
                m.IsActive
                && m.TenantId == legacyId
                && userIdsWithDemo.Contains(m.UserId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (legacyRows.Count == 0)
            return 0;

        var now = DateTime.UtcNow;
        foreach (var row in legacyRows)
        {
            row.IsActive = false;
            row.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return legacyRows.Count;
    }
}
