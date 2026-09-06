using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

public interface IBackupRetentionPolicyService
{
    Task<BackupRetentionPolicySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<BackupRetentionPolicyResponseDto> GetAsync(
        bool includeCosts,
        BackupRunAccessScope? accessScope,
        CancellationToken cancellationToken = default);

    Task<BackupRetentionPolicyResponseDto> UpdateAsync(
        BackupRetentionPolicyPutRequestDto dto,
        string? actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task ApplyDefaultLegalHoldIfRequiredAsync(
        BackupRun run,
        string? actorUserId,
        CancellationToken cancellationToken = default);
}
