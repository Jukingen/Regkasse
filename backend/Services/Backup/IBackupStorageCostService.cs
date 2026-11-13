using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

public interface IBackupStorageCostService
{
    Task<BackupStorageCostResponseDto> GetAsync(
        BackupRunAccessScope? accessScope = null,
        CancellationToken cancellationToken = default);
}
