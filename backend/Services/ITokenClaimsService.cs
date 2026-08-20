using System.Security.Claims;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Builds compact JWT claims: <c>sub</c>, <c>userId</c>, <c>email</c>, one <c>role</c> per assigned role,
/// optional <c>tenant_id</c>/<c>branch_id</c>/<c>app_context</c>. Permission catalog is omitted for canonical
/// system roles (SuperAdmin keeps <c>system.critical</c>); custom roles still embed filtered permission claims.
/// </summary>
public interface ITokenClaimsService
{
    /// <param name="tenantId">Optional; when set, adds tenant_id claim for scope checks.</param>
    /// <param name="branchId">Optional; when set, adds branch_id claim for branch scope checks.</param>
    /// <param name="appContext">Optional; when set, adds app_context claim ("pos" | "admin").</param>
    Task<IReadOnlyList<Claim>> BuildClaimsAsync(
        ApplicationUser user,
        IList<string> roles,
        string? tenantId = null,
        string? branchId = null,
        string? appContext = null,
        CancellationToken cancellationToken = default);
}
