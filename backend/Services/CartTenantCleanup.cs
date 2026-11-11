using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Tenant-scoped cart deletion. Cart rows are not <see cref="ITenantEntity"/>;
/// ownership is the cart user's active membership in the ambient tenant
/// (global query filter on <see cref="UserTenantMembership"/>). No
/// <c>IgnoreQueryFilters()</c>.
/// </summary>
internal static class CartTenantCleanup
{
    internal const int IdleMinutes = 45;

    public static async Task<List<string>> ActiveMemberUserIdsAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        return await context.UserTenantMemberships.AsNoTracking()
            .Where(m => m.IsActive)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes active carts of the ambient tenant that have been idle for
    /// <see cref="IdleMinutes"/>. Idle is <see cref="BaseEntity.UpdatedAt"/>,
    /// falling back to <see cref="BaseEntity.CreatedAt"/> when never updated.
    /// </summary>
    public static async Task<int> DeleteIdleActiveCartsAsync(
        AppDbContext context,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var userIds = await ActiveMemberUserIdsAsync(context, cancellationToken).ConfigureAwait(false);
        if (userIds.Count == 0)
            return 0;

        var cutoff = utcNow.AddMinutes(-IdleMinutes);
        var carts = await context.Carts
            .Include(c => c.Items)
            .Where(c => c.Status == CartStatus.Active
                        && userIds.Contains(c.UserId)
                        && ((c.UpdatedAt != null && c.UpdatedAt < cutoff)
                            || (c.UpdatedAt == null && c.CreatedAt < cutoff)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (carts.Count == 0)
            return 0;

        context.CartItems.RemoveRange(carts.SelectMany(c => c.Items));
        context.Carts.RemoveRange(carts);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return carts.Count;
    }
}
