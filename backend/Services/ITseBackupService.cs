using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>
/// Tenant-scoped full TSE disaster-recovery snapshots
/// (device inventory incl. failover roles + signature chain + BelegNr sequences).
/// Vendor private keys stay outside the package. Restore refuses chain downgrades unless forced.
/// </summary>
public interface ITseBackupService
{
    Task<CreateTseBackupResponseDto> CreateTseBackupAsync(
        Guid tenantId,
        string? actorUserId,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseBackupListItemDto>> ListBackupsAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<TseBackupRestorePreviewDto?> PreviewRestoreAsync(
        Guid backupId,
        CancellationToken cancellationToken = default);

    Task<RestoreTseBackupResponseDto> RestoreTseBackupAsync(
        Guid backupId,
        RestoreTseBackupRequestDto request,
        string? actorUserId,
        CancellationToken cancellationToken = default);
}
