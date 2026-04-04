using System.Security.Claims;

namespace KasseAPI_Final.Services;

/// <summary>
/// Checks tenant/branch scope from user claims when <c>tenant_id</c> / <c>branch_id</c> are present on the JWT.
/// Login/refresh may omit these claims until multi-tenant wiring is complete; scope helpers treat missing branch as unconstrained (see implementation).
/// </summary>
public interface IScopeCheckService
{
    bool IsInScope(ClaimsPrincipal user, string? requiredTenantId, string? requiredBranchId);
    string? GetCurrentTenantId(ClaimsPrincipal user);
    string? GetCurrentBranchId(ClaimsPrincipal user);

    /// <summary>True if user can access the order (branch match and optionally assigned waiter).</summary>
    bool CanAccessOrder(ClaimsPrincipal user, string? orderAssignedUserId, string? orderBranchId);

    /// <summary>True if user can access the cash register (branch match and optionally assigned user).</summary>
    bool CanAccessCashRegister(ClaimsPrincipal user, string? registerAssignedUserId, string? registerBranchId);

    /// <summary>True if user can access the branch (no branch claim = all, or claim equals branchId).</summary>
    bool CanAccessBranch(ClaimsPrincipal user, string branchId);
}
