using KasseAPI_Final.Services.AdminTenants;

namespace KasseAPI_Final.Services.Tenancy;

/// <summary>Permanent tenant delete dependency summary and validation (no mutation).</summary>
public interface ITenantDeletionService
{
    Task<TenantDeleteDependenciesDto> GetDependencySummaryAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<(bool Success, string? ErrorCode, string? ErrorMessage)> ValidateHardDeleteAsync(
        Guid tenantId,
        bool forceDelete = false,
        CancellationToken ct = default);
}
