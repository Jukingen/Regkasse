using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

public interface IBackupSettingsAdminService
{
    Task<BackupSettingsResponseDto> GetAsync(CancellationToken cancellationToken = default);

    Task<BackupSettingsResponseDto> PutAsync(BackupSettingsPutRequestDto dto, CancellationToken cancellationToken = default);

    Task<BackupScheduleStatusResponseDto> GetScheduleStatusAsync(CancellationToken cancellationToken = default);
}
