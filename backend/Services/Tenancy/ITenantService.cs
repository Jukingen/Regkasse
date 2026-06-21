using KasseAPI_Final.Services.AdminTenants;

namespace KasseAPI_Final.Services.Tenancy;

/// <summary>Tenant lifecycle (soft delete, restore, permanent delete) for SaaS mandants.</summary>
public interface ITenantService
{
    Task<(bool Success, string? Error)> SoftDeleteAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> RestoreAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<TenantPermanentDeleteResult> HardDeleteAsync(
        Guid tenantId,
        HardDeleteAdminTenantRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default);
}
