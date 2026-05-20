namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Provisions <see cref="Models.UserTenantMembership"/> after Identity user creation (or seed).
/// Tenant id must exist in <c>tenants</c>; never use unchecked client input without server validation.
/// </summary>
public interface IUserTenantMembershipProvisioner
{
    /// <summary>
    /// Ensures the user has exactly one active membership for <paramref name="tenantId"/>.
    /// Idempotent when already active for that tenant. Deactivates other active rows when switching.
    /// </summary>
    /// <exception cref="InvalidOperationException">No row in <c>tenants</c> for <paramref name="tenantId"/>.</exception>
    Task ProvisionActiveMembershipAsync(
        string userId,
        Guid tenantId,
        bool isOwner = false,
        CancellationToken cancellationToken = default);
}
