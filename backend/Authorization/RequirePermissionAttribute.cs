using Microsoft.AspNetCore.Authorization;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Authorize by permission name. Maps to policy "Permission:{permission}".
/// Use with PermissionRequirement + PermissionAuthorizationHandler.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
        : base(PermissionCatalog.PolicyPrefix + permission)
    {
        if (string.IsNullOrEmpty(permission))
            throw new ArgumentNullException(nameof(permission));
    }
}
