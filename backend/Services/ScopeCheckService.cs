using System.Security.Claims;
using KasseAPI_Final.Authorization;

namespace KasseAPI_Final.Services;

/// <summary>
/// Reads tenant_id and branch_id from claims; supports scope checks for resource access.
/// Waiter: own order (assigned user or branch). Cashier: own till (assigned user or branch). Manager: own branch.
/// </summary>
public sealed class ScopeCheckService : IScopeCheckService
{
    public const string TenantIdClaim = "tenant_id";
    public const string BranchIdClaim = "branch_id";

    private static string? GetUserId(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("user_id")?.Value;

    private static string? GetRole(ClaimsPrincipal user) =>
        user.FindFirst("role")?.Value ?? user.FindFirst(ClaimTypes.Role)?.Value;

    public bool IsInScope(ClaimsPrincipal user, string? requiredTenantId, string? requiredBranchId)
    {
        if (requiredTenantId != null && GetCurrentTenantId(user) != requiredTenantId)
            return false;
        if (requiredBranchId != null && GetCurrentBranchId(user) != requiredBranchId)
            return false;
        return true;
    }

    public string? GetCurrentTenantId(ClaimsPrincipal user) =>
        user.FindFirst(TenantIdClaim)?.Value;

    public string? GetCurrentBranchId(ClaimsPrincipal user) =>
        user.FindFirst(BranchIdClaim)?.Value;

    /// <inheritdoc />
    public bool CanAccessOrder(ClaimsPrincipal user, string? orderAssignedUserId, string? orderBranchId)
    {
        var userBranchId = GetCurrentBranchId(user);
        var userId = GetUserId(user);
        var role = GetRole(user);

        if (string.IsNullOrEmpty(userId)) return false;

        if (string.IsNullOrEmpty(userBranchId))
            return true;

        if (!string.IsNullOrEmpty(orderBranchId) && userBranchId != orderBranchId)
            return false;

        if (!string.IsNullOrEmpty(orderAssignedUserId) && orderAssignedUserId == userId)
            return true;

        if (string.Equals(role, Roles.Manager, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <inheritdoc />
    public bool CanAccessCashRegister(ClaimsPrincipal user, string? registerAssignedUserId, string? registerBranchId)
    {
        var userBranchId = GetCurrentBranchId(user);
        var userId = GetUserId(user);

        if (string.IsNullOrEmpty(userId)) return false;

        if (string.IsNullOrEmpty(userBranchId))
            return true;

        if (!string.IsNullOrEmpty(registerBranchId) && userBranchId != registerBranchId)
            return false;

        if (!string.IsNullOrEmpty(registerAssignedUserId) && registerAssignedUserId == userId)
            return true;

        var role = GetRole(user);
        if (string.Equals(role, Roles.Manager, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <inheritdoc />
    public bool CanAccessBranch(ClaimsPrincipal user, string branchId)
    {
        if (string.IsNullOrEmpty(branchId)) return true;
        var userBranchId = GetCurrentBranchId(user);
        if (string.IsNullOrEmpty(userBranchId)) return true;
        return string.Equals(userBranchId, branchId, StringComparison.OrdinalIgnoreCase);
    }
}
