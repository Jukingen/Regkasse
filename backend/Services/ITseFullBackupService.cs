using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>
/// Product-facing aliases for full TSE DR backup/restore
/// (same implementation as <see cref="ITseBackupService"/> / <c>tse_backups</c>).
/// </summary>
public interface ITseFullBackupService
{
    Task<CreateTseBackupResponseDto> CreateFullBackupAsync(
        Guid tenantId,
        string? actorUserId = null,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<RestoreTseBackupResponseDto> RestoreFromBackupAsync(
        Guid backupId,
        RestoreTseBackupRequestDto request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseBackupListItemDto>> ListBackupsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
