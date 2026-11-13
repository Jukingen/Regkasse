using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Validates backup package content (manifest row counts vs live DB + fiscal integrity checks).
/// Persists a <c>backup_verifications</c> row (<c>content_validation</c>).
/// Not restore proof and not SHA-256 integrity (see <see cref="IBackupChecksumVerificationService"/>).
/// </summary>
public interface IBackupContentValidationService
{
    public const string VerifierSourceContentValidation = "content_validation";

    /// <summary>
    /// Returns the latest persisted content-validation report for the run when available;
    /// otherwise runs <see cref="ValidateContentAsync"/> and persists a new row.
    /// </summary>
    Task<BackupContentValidationDto> GetOrRunValidationAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    /// <summary>Always re-validates manifest/content and persists verification evidence.</summary>
    Task<BackupContentValidationDto> ValidateContentAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    /// <summary>Alias of <see cref="ValidateContentAsync"/>.</summary>
    Task<BackupContentValidationDto> ValidateAsync(
        Guid backupRunId,
        CancellationToken cancellationToken = default) =>
        ValidateContentAsync(backupRunId, cancellationToken);
}
