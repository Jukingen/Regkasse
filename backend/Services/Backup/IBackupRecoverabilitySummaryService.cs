using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Operatör özeti: son istek vs son bilinen iyi yedek / doğrulama / restore kanıtı.
/// </summary>
public interface IBackupRecoverabilitySummaryService
{
    Task<BackupRecoverabilitySummaryResponseDto> GetAsync(CancellationToken cancellationToken = default);
}
