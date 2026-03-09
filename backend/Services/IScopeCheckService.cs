using System.Security.Claims;

namespace KasseAPI_Final.Services;

/// <summary>
/// Checks tenant/branch scope from user claims. Token claims tenant_id/branch_id are set in ITokenClaimsService.
/// Use for: waiter own order, cashier own till, manager own branch.
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
