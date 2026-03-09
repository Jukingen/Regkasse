using Microsoft.AspNetCore.Authorization;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Authorization requirement that demands a specific permission (e.g. report.view).
/// Evaluated by PermissionAuthorizationHandler using role-permission matrix.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }
}
