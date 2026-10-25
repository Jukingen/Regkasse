using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Evaluates the Backup/DR known-gap catalog for one mandant.
/// Acceptance changes the card color to amber. It does not clear the check.
/// </summary>
public interface IBackupKnownGapStatusService
{
    Task<BackupKnownGapViewDto> GetViewAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
