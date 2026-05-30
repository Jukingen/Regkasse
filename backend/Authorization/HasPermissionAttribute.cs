using Microsoft.AspNetCore.Authorization;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Authorize by permission name. Maps to policy <see cref="PermissionCatalog.PolicyPrefix"/> + permission
/// (e.g. <c>Permission:user.view</c>). Evaluated by <see cref="PermissionAuthorizationHandler"/> using JWT claims,
/// <see cref="KasseAPI_Final.Services.IPermissionService"/> (roles + user overrides), and role-matrix fallback.
/// Example: <c>[HasPermission(AppPermissions.UserView)]</c>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public string Permission { get; }

    public HasPermissionAttribute(string permission)
        : base(PermissionCatalog.PolicyPrefix + permission)
    {
        if (string.IsNullOrEmpty(permission))
            throw new ArgumentNullException(nameof(permission));
        Permission = permission;
    }
}
