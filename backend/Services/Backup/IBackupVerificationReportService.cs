using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

public interface IBackupVerificationReportService
{
    Task<BackupVerificationReportDto> GenerateReportAsync(Guid backupRunId, CancellationToken cancellationToken = default);
}
