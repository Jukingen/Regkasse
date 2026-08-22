using System.Security.Claims;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Evaluates app permissions from JWT permission claims and role-to-permission matrix.
/// Shared by cash-register resolution, POS readiness, and related services.
/// </summary>
public static class PermissionClaimHelper
{
    private const string RoleClaimType = "role";
    private const string LegacyRoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    public static bool PrincipalHasAppPermission(ClaimsPrincipal? user, string permission)
    {
        if (user == null || string.IsNullOrEmpty(permission))
            return false;

        var permissionClaims = user.Claims
            .Where(c => string.Equals(c.Type, PermissionCatalog.PermissionClaimType, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (permissionClaims.Count > 0)
            return PermissionImplication.IsSatisfied(permission, permissionClaims);

        var roles = GetRolesFromPrincipal(user);
        var fromRoles = RolePermissionMatrix.GetPermissionsForRoles(roles);
        var appContext = user.FindFirst(ClientAppPolicy.AppContextClaimType)?.Value;
        var filtered = AdminAppPermissionProfile.Filter(appContext, roles, fromRoles);
        return PermissionImplication.IsSatisfied(permission, filtered);
    }

    /// <summary>
    /// True for platform Super Admins. Accepts the role claim in any of its supported shapes and also
    /// <see cref="AppPermissions.SystemCritical"/>, which is all a compact Super Admin JWT carries.
    /// </summary>
    public static bool IsSuperAdminPrincipal(ClaimsPrincipal? user)
    {
        if (user == null)
            return false;

        if (user.IsInRole(Roles.SuperAdmin))
            return true;

        foreach (var role in GetRolesFromPrincipal(user))
        {
            if (string.Equals(role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return PrincipalHasAppPermission(user, AppPermissions.SystemCritical);
    }

    public static IReadOnlyList<string> GetRolesFromPrincipal(ClaimsPrincipal user)
    {
        var list = new List<string>();
        foreach (var claim in user.Claims)
        {
            if (string.Equals(claim.Type, RoleClaimType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, LegacyRoleClaimType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))
            {
                var v = claim.Value?.Trim();
                if (!string.IsNullOrEmpty(v))
                    list.Add(v);
            }
        }

        return list;
    }
}
