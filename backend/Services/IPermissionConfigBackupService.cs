using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public interface IPermissionConfigBackupService
{
    Task<PermissionConfigBackupListItemDto> CreateAsync(
        CreatePermissionConfigBackupRequest? request,
        string? actorUserId,
        string trigger = PermissionConfigBackupTriggers.Manual,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionConfigBackupListItemDto>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<PermissionConfigRestorePreviewDto?> PreviewRestoreAsync(
        Guid backupId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Code, string? Error)> RestoreAsync(
        Guid backupId,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<PermissionConfigBackupSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<PermissionConfigBackupSettingsDto> SetSettingsAsync(
        PermissionConfigBackupSettingsDto settings,
        CancellationToken cancellationToken = default);

    Task TryAutoBackupBeforeChangeAsync(
        string? actorUserId,
        CancellationToken cancellationToken = default);
}
