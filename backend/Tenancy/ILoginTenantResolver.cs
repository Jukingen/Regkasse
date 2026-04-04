namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Resolves the effective tenant snapshot at password login (before JWT exists).
/// Primary source: active <see cref="Models.UserTenantMembership"/> rows.
/// Legacy: no active membership → seeded default tenant (single-tenant deployments).
/// </summary>
public interface ILoginTenantResolver
{
    /// <summary>
    /// Active membership count 0 → legacy default tenant snapshot.
    /// Exactly 1 → that tenant.
    /// More than 1 → should not occur (DB partial unique); first by <see cref="Models.UserTenantMembership.CreatedAtUtc"/> with critical log.
    /// </summary>
    Task<AuthTenantSnapshot> ResolveSnapshotForLoginAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>True if the user has at least one active membership row.</summary>
    Task<bool> HasActiveMembershipAsync(string userId, CancellationToken cancellationToken = default);
}
