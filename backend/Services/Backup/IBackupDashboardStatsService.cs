using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

public interface IBackupDashboardStatsService
{
    Task<BackupDashboardStatsResponseDto> GetAsync(
        BackupRunAccessScope? accessScope = null,
        CancellationToken cancellationToken = default);

    /// <summary>Widget-focused health projection derived from <see cref="GetAsync"/>.</summary>
    Task<BackupDashboardHealthResponseDto> GetHealthAsync(
        BackupRunAccessScope? accessScope = null,
        CancellationToken cancellationToken = default);
}
