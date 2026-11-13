using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Maintenance;

/// <summary>
/// Platform maintenance mode: enforces API 503 / POS payment gates while a window is active.
/// Built on top of <see cref="IMaintenanceNotificationService"/> InProgress / in-window notices.
/// </summary>
public interface IMaintenanceModeService
{
    Task<MaintenanceModeStatusDto> GetCurrentStatusAsync(CancellationToken cancellationToken = default);

    Task<MaintenanceModeStatusDto> StartAsync(
        string actorUserId,
        StartMaintenanceModeRequestDto request,
        CancellationToken cancellationToken = default);

    Task<MaintenanceModeStatusDto> EndAsync(
        string actorUserId,
        CancellationToken cancellationToken = default);
}
