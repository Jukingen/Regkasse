using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Deployment;

public interface ITenantDeploymentService
{
    Task<DeploymentOverallStatusDto> GetOverallStatusAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantDeploymentHistoryDto>> ListLatestPerTenantAsync(
        CancellationToken cancellationToken = default);

    Task<TenantDeploymentHistoryDto?> GetLatestForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TenantDeploymentHistoryDto> RecordAsync(
        TenantDeploymentRecordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Record one history row per tenant id/slug from a CI canary report.</summary>
    Task RecordFromCiAsync(
        IReadOnlyList<string> tenantIdsOrSlugs,
        string version,
        string stage,
        string status,
        string? gitSha,
        string? runUrl,
        string? triggeredBy,
        bool? smokePassed,
        string? errorMessage,
        int? soakHours,
        CancellationToken cancellationToken = default);

    Task<DeploymentRollbackResultDto> RollbackTenantAsync(
        Guid tenantId,
        TenantDeploymentRollbackRequest request,
        string actor,
        CancellationToken cancellationToken = default);
}
