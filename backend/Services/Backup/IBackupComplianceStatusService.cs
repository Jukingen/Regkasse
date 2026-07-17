using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Backup;

namespace KasseAPI_Final.Services.Backup;

public interface IBackupComplianceStatusService
{
    /// <summary>
    /// 30-day succeeded-run restore-readiness rollup (SHA-256 present, System strategy for pg_restore path).
    /// </summary>
    Task<BackupComplianceStatusResponseDto> GetAsync(
        BackupRunAccessScope? accessScope = null,
        CancellationToken cancellationToken = default);
}
