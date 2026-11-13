using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

public interface IBackupColdArchiveService
{
    Task<BackupMoveToColdResponseDto> MoveRunToColdAsync(
        BackupRun run,
        string? actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task<int> ArchiveAgedSucceededRunsAsync(CancellationToken cancellationToken = default);
}
