using System.Security.Claims;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Builds claims for JWT (and cookie) at login/refresh: sub, name, role, permission, optional tenant_id/branch_id.
/// Role and permissions from Identity and RolePermissionMatrix (Roles.* only; no legacy alias).
/// </summary>
public interface ITokenClaimsService
{
    /// <summary>
    /// Builds the full claim list: sub, email, name, user_id, role, roles, permission (one per permission),
    /// optional tenant_id, branch_id, and app_context.
    /// Uses role normalization (trim) and RolePermissionMatrix for permissions.
    /// </summary>
    /// <param name="tenantId">Optional; when set, adds tenant_id claim for scope checks.</param>
    /// <param name="branchId">Optional; when set, adds branch_id claim for scope checks.</param>
    /// <param name="appContext">Optional; when set, adds app_context claim ("pos" | "admin").</param>
    Task<IReadOnlyList<Claim>> BuildClaimsAsync(
        ApplicationUser user,
        IList<string> roles,
        string? tenantId = null,
        string? branchId = null,
        string? appContext = null,
        CancellationToken cancellationToken = default);
}
