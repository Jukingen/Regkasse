using System.Security.Claims;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Evaluates <see cref="PermissionRequirement"/> for policies registered via <see cref="HasPermissionAttribute"/>.
/// 1. JWT <c>permission</c> claims + <see cref="PermissionImplication"/> (fast path).
/// 2. <see cref="IPermissionService.HasPermissionAsync"/> (roles + user overrides from DB).
/// 3. Role claims + <see cref="RolePermissionMatrix"/> fallback (unit tests / legacy tokens without permission claims).
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private const string RoleClaimType = "role";
    private const string LegacyRoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    private readonly IServiceScopeFactory _scopeFactory;

    public PermissionAuthorizationHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var user = context.User;
        var permissionClaims = user.Claims
            .Where(c => string.Equals(c.Type, PermissionCatalog.PermissionClaimType, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (permissionClaims.Count > 0
            && PermissionImplication.IsSatisfied(requirement.Permission, permissionClaims))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = user.GetActorUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            using var scope = _scopeFactory.CreateScope();
            var permissionService = scope.ServiceProvider.GetService<IPermissionService>();
            if (permissionService != null)
            {
                var tenantAccessor = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
                if (await permissionService.HasPermissionAsync(
                        userId,
                        requirement.Permission,
                        tenantAccessor.TenantId))
                {
                    context.Succeed(requirement);
                    return;
                }
            }
        }

        var roles = GetRolesFromContext(user);
        if (roles.Count == 0)
            return;

        var permissions = RolePermissionMatrix.GetPermissionsForRoles(roles);
        if (PermissionImplication.IsSatisfied(requirement.Permission, permissions))
            context.Succeed(requirement);
    }

    private static IReadOnlyList<string> GetRolesFromContext(ClaimsPrincipal user)
    {
        var list = new List<string>();

        foreach (var claim in user.Claims)
        {
            if (string.Equals(claim.Type, RoleClaimType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, LegacyRoleClaimType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))
            {
                var value = claim.Value?.Trim();
                if (!string.IsNullOrEmpty(value))
                    list.Add(value);
            }
        }

        return list;
    }
}
