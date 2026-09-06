using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

public interface IBackupChainService
{
    Task<BackupChainResponseDto> GetChainAsync(
        Guid? tenantId,
        BackupRunAccessScope scope,
        CancellationToken cancellationToken = default);
}
