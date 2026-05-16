using System.Security.Claims;

namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Resolves the effective tenant (and optional branch placeholders) for auth JSON and JWT emission.
/// Wave 0–1: uses <c>tenant_id</c> claim when present and valid; otherwise the seeded legacy default tenant.
/// Password login tenant selection is <see cref="ILoginTenantResolver"/> (membership first).
/// Branch fields stay null until branch rollout.
/// </summary>
public interface IAuthTenantSnapshotProvider
{
    /// <summary>Snapshot for /me and callers with only JWT context (no session row).</summary>
    Task<AuthTenantSnapshot> GetSnapshotAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Source-of-truth ordering for access-token issuance: valid persisted session tenant → valid JWT tenant claim → legacy default.
    /// </summary>
    Task<AuthTenantSnapshot> ResolveForTokenIssuanceAsync(
        string? persistedSessionTenantId,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default);
}

/// <param name="TenantId">Canonical string form of the tenant Guid (JWT claim value).</param>
/// <param name="TenantSlug">Stable tenant key for dev host routing (<see cref="SubdomainTenantProvider.DevTenantHeaderName"/>).</param>
public readonly record struct AuthTenantSnapshot(
    string TenantId,
    string? TenantDisplayName,
    string? TenantSlug,
    string? BranchId,
    string? BranchDisplayName);
