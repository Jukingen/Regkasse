using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KasseAPI_Final.Tenancy;

/// <inheritdoc />
public sealed class UserTenantMembershipProvisioner : IUserTenantMembershipProvisioner
{
    private readonly AppDbContext _db;
    private readonly ILogger<UserTenantMembershipProvisioner> _logger;

    public UserTenantMembershipProvisioner(
        AppDbContext db,
        ILogger<UserTenantMembershipProvisioner>? logger = null)
    {
        _db = db;
        _logger = logger ?? NullLogger<UserTenantMembershipProvisioner>.Instance;
    }

    /// <inheritdoc />
    public async Task ProvisionActiveMembershipAsync(
        string userId,
        Guid tenantId,
        bool isOwner = false,
        CancellationToken cancellationToken = default)
    {
        tenantId = await ResolveBusinessTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var tenantOk = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!tenantOk)
            throw new InvalidOperationException($"Tenant '{tenantId:D}' does not exist; membership not provisioned.");

        var existingForPair = await MembershipsUnfiltered()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        var activeForUser = await MembershipsUnfiltered()
            .Where(m => m.UserId == userId && m.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existingForPair != null
            && existingForPair.IsActive
            && activeForUser.Count == 1
            && activeForUser[0].Id == existingForPair.Id
            && !isOwner)
        {
            return;
        }

        foreach (var m in activeForUser)
        {
            m.IsActive = false;
            m.UpdatedAtUtc = DateTime.UtcNow;
        }

        UserTenantMembership target;
        if (existingForPair != null)
        {
            existingForPair.IsActive = true;
            existingForPair.UpdatedAtUtc = DateTime.UtcNow;
            target = existingForPair;
        }
        else
        {
            target = new UserTenantMembership
            {
                UserId = userId,
                TenantId = tenantId,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            };
            _db.UserTenantMemberships.Add(target);
        }

        if (isOwner)
        {
            var otherOwners = await MembershipsUnfiltered()
                .Where(m => m.TenantId == tenantId && m.IsOwner && m.Id != target.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var m in otherOwners)
            {
                m.IsOwner = false;
                m.UpdatedAtUtc = DateTime.UtcNow;
            }

            target.IsOwner = true;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Platform / legacy-default is not a business mandant — redirect membership writes to demo <c>dev</c>.
    /// </summary>
    private async Task<Guid> ResolveBusinessTenantIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!SystemTenantIds.IsPlatformTenantId(tenantId))
            return tenantId;

        _logger.LogWarning(
            "Attempted to create membership for default/platform tenant {PlatformTenantId}. Redirecting to demo tenant {DevTenantId}.",
            SystemTenantIds.Platform,
            DemoTenantIds.Dev);

        var devId = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == DemoTenantIds.Dev || t.Slug == "dev")
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (devId is null || devId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Dev tenant not found. Cannot assign default/platform membership.");
        }

        return devId.Value;
    }

    /// <summary>Membership rows are keyed by explicit <paramref name="tenantId"/>; ignore ambient tenant filter.</summary>
    private IQueryable<UserTenantMembership> MembershipsUnfiltered() =>
        _db.UserTenantMemberships.IgnoreQueryFilters();
}
