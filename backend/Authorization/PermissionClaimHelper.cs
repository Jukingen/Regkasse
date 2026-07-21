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
        return PermissionImplication.IsSatisfied(permission, fromRoles);
    }

    public static IReadOnlyList<string> GetRolesFromPrincipal(ClaimsPrincipal user)
    {
        var list = new List<string>();
        foreach (var claim in user.Claims)
        {
            if (string.Equals(claim.Type, RoleClaimType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, LegacyRoleClaimType, StringComparison.OrdinalIgnoreCase))
            {
                var v = claim.Value?.Trim();
                if (!string.IsNullOrEmpty(v))
                    list.Add(v);
            }
        }

        return list;
    }
}
