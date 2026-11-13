using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Super Admin TSE resource pools for multi-tenant capacity grouping (not fiscal signing).
/// </summary>
public interface ITseResourcePoolService
{
    Task<TseResourcePoolDto> CreateResourcePoolAsync(
        CreateTseResourcePoolRequestDto request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseResourcePoolDto>> ListResourcePoolsAsync(
        CancellationToken cancellationToken = default);

    Task<TsePoolAssignmentResultDto> AssignTenantToPoolAsync(
        Guid tenantId,
        Guid poolId,
        int reservedCapacity = 1,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TsePoolAssignmentResultDto> UnassignTenantAsync(
        Guid tenantId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TsePoolStatusDto> GetPoolStatusAsync(
        Guid poolId,
        CancellationToken cancellationToken = default);

    Task<TsePoolMetricsDto> GetPoolMetricsAsync(
        Guid poolId,
        CancellationToken cancellationToken = default);

    Task<TseResourcePoolDto> GetPoolAsync(
        Guid poolId,
        CancellationToken cancellationToken = default);
}
