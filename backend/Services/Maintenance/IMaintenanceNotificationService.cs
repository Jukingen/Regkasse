using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Maintenance;

public interface IMaintenanceNotificationService
{
    Task<MaintenanceNotificationDto> CreateAsync(
        string createdByUserId,
        CreateMaintenanceNotificationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<MaintenanceNotificationDto?> UpdateAsync(
        Guid id,
        UpdateMaintenanceNotificationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<MaintenanceNotificationDto?> PublishAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<MaintenanceNotificationDto?> CancelAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<MaintenanceNotificationDto?> MarkInProgressAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<MaintenanceNotificationDto?> CompleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<MaintenanceNotificationListResponseDto> ListAsync(
        string? status,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Active notices for a client surface (FA / POS), filtered by acknowledgments and force-display rules.
    /// </summary>
    Task<IReadOnlyList<MaintenanceNotificationDto>> GetActiveForUserAsync(
        string userId,
        string surface,
        CancellationToken cancellationToken = default);

    Task<MaintenanceNotificationDto?> AcknowledgeAsync(
        Guid id,
        string userId,
        AcknowledgeMaintenanceNotificationRequestDto request,
        CancellationToken cancellationToken = default);
}
