using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Rules for whether a user still belongs to an operational tenant after soft-delete / suspension.
/// </summary>
public static class OperationalTenantMembershipPolicy
{
    public static bool IsOperationalTenant(Tenant tenant) =>
        tenant.IsActive && tenant.Status != TenantStatuses.Deleted;

    public static bool IsSuperAdmin(ApplicationUser user) =>
        string.Equals(user.Role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase);

    public static bool HasActiveOperationalMembership(ApplicationUser user) =>
        user.UserTenantMemberships.Any(m =>
            m.IsActive && m.Tenant != null && IsOperationalTenant(m.Tenant));

    public static bool HasActiveBusinessMembership(
        ApplicationUser user,
        IReadOnlySet<Guid> businessTenantIds) =>
        user.UserTenantMemberships.Any(m =>
            m.IsActive
            && m.Tenant != null
            && IsOperationalTenant(m.Tenant)
            && businessTenantIds.Contains(m.TenantId));

    /// <summary>Non–Super Admin with no active membership on any operational tenant.</summary>
    public static bool IsOrphanedTenantUser(ApplicationUser user) =>
        !IsSuperAdmin(user) && !HasActiveOperationalMembership(user);

    /// <summary>Super Admin or operator with a non-business tenant membership only.</summary>
    public static bool IsPlatformOperator(
        ApplicationUser user,
        IReadOnlySet<Guid> businessTenantIds) =>
        IsSuperAdmin(user)
        || (HasActiveOperationalMembership(user)
            && !HasActiveBusinessMembership(user, businessTenantIds));
}
