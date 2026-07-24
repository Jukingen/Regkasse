using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Zero-downtime TSE catalog / policy update orchestration (diagnostic — not fiscal firmware flash).
/// </summary>
public interface ITseUpdateService
{
    Task<TseUpdateStatusDto> CheckForUpdatesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TseUpdateResultDto> ApplyUpdateAsync(
        Guid tenantId,
        string updateType,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseUpdateHistoryDto> GetUpdateHistoryAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
