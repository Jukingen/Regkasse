using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

public interface IPitrService
{
    /// <summary>
    /// Restore window from succeeded <c>backup_runs</c> (deployment-wide; optional tenant hint via idempotency key).
    /// </summary>
    Task<PitrAvailabilityResponseDto> GetPitrAvailabilityAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    Task<RestorePointValidationResultDto> ValidateRestorePointAsync(
        Guid? tenantId,
        DateTime targetTimeUtc,
        CancellationToken cancellationToken = default);
}
