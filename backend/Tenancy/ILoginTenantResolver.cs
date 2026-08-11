namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Resolves the effective tenant snapshot at password login (before JWT exists).
/// Primary source: active <see cref="Models.UserTenantMembership"/> rows.
/// Fallback when no membership: seeded <c>dev</c> when present, else <see cref="SystemTenantIds.Platform"/>.
/// </summary>
public interface ILoginTenantResolver
{
    /// <summary>
    /// Active membership count 0 → fallback snapshot (<c>dev</c> preferred, else platform).
    /// Exactly 1 → that tenant.
    /// More than 1 → prefer <c>dev</c>, else oldest non-platform, with critical log.
    /// </summary>
    Task<AuthTenantSnapshot> ResolveSnapshotForLoginAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>True if the user has at least one active membership row.</summary>
    Task<bool> HasActiveMembershipAsync(string userId, CancellationToken cancellationToken = default);
}
