using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Operatör recoverability özeti: <c>GET /api/admin/backup/recoverability-summary</c>.
/// Son yedek isteği (<see cref="BackupRecoverabilitySummaryResponseDto.LatestRunAt"/> / <c>LatestRunStatus</c>)
/// ile son başarılı yedek, artifact doğrulama ve <em>yalnızca zamanlanmış</em> başarılı restore drill kanıtı ayrı döner;
/// yaş alanları <see cref="System.TimeProvider"/> UTC “şimdi” ile hesaplanır.
/// </summary>
public interface IBackupRecoverabilitySummaryService
{
    Task<BackupRecoverabilitySummaryResponseDto> GetAsync(
        BackupRunAccessScope? accessScope = null,
        CancellationToken cancellationToken = default);
}
