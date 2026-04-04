using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Tenancy;

/// <inheritdoc />
public sealed class UserTenantMembershipProvisioner : IUserTenantMembershipProvisioner
{
    private readonly AppDbContext _db;

    public UserTenantMembershipProvisioner(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task ProvisionActiveMembershipAsync(string userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenantOk = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!tenantOk)
            throw new InvalidOperationException($"Tenant '{tenantId:D}' does not exist; membership not provisioned.");

        var existingForPair = await _db.UserTenantMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        var activeForUser = await _db.UserTenantMemberships
            .Where(m => m.UserId == userId && m.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existingForPair != null && existingForPair.IsActive && activeForUser.Count == 1 && activeForUser[0].Id == existingForPair.Id)
            return;

        foreach (var m in activeForUser)
        {
            m.IsActive = false;
            m.UpdatedAtUtc = DateTime.UtcNow;
        }

        if (existingForPair != null)
        {
            existingForPair.IsActive = true;
            existingForPair.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            _db.UserTenantMemberships.Add(new UserTenantMembership
            {
                UserId = userId,
                TenantId = tenantId,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
