namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Resolves the effective tenant for singleton settings reads/writes.
/// Uses the same rules as <see cref="IAuthTenantSnapshotProvider"/> (JWT <c>tenant_id</c> when valid, else legacy default).
/// </summary>
public interface ISettingsTenantResolver
{
    /// <summary>Returns the tenant id used for settings persistence in this request.</summary>
    Task<Guid> ResolveEffectiveTenantIdAsync(CancellationToken cancellationToken = default);
}
