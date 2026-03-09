using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Evaluates PermissionRequirement: first checks "permission" claims (from login token), then falls back to role-derived permissions via RolePermissionMatrix.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private const string RoleClaimType = "role";
    private const string LegacyRoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var user = context.User;
        var permissionClaims = user.Claims.Where(c => string.Equals(c.Type, PermissionCatalog.PermissionClaimType, StringComparison.OrdinalIgnoreCase)).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (permissionClaims.Count > 0)
        {
            if (permissionClaims.Contains(requirement.Permission))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
            return Task.CompletedTask;
        }

        var roles = GetRolesFromContext(user);
        if (roles.Count == 0)
            return Task.CompletedTask;

        var permissions = RolePermissionMatrix.GetPermissionsForRoles(roles);
        if (permissions.Contains(requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }

    private static IReadOnlyList<string> GetRolesFromContext(ClaimsPrincipal user)
    {
        var list = new List<string>();

        foreach (var claim in user.Claims)
        {
            if (string.Equals(claim.Type, RoleClaimType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, LegacyRoleClaimType, StringComparison.OrdinalIgnoreCase))
            {
                var value = claim.Value?.Trim();
                if (!string.IsNullOrEmpty(value))
                    list.Add(value);
            }
        }

        return list;
    }
}
