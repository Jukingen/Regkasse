using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// On-demand / scheduled on-disk SHA-256 re-verification of backup artifacts.
/// Persists evidence in <c>backup_verifications</c>; not restore proof.
/// </summary>
public interface IBackupChecksumVerificationService
{
    public const string VerifierSourceOnDemandHttp = "on_demand_http";
    public const string VerifierSourceScheduledReverify = "scheduled_reverify";

    /// <summary>
    /// Re-hash every artifact for the run (staging or external archive), persist a verification row, return DTO.
    /// </summary>
    Task<BackupChecksumVerifyResponseDto> VerifyAndPersistAsync(
        Guid backupRunId,
        string verifierSource,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// On-demand HTTP path: same as <see cref="VerifyAndPersistAsync"/> with
    /// <see cref="VerifierSourceOnDemandHttp"/>.
    /// </summary>
    Task<BackupChecksumVerifyResponseDto> VerifyChecksumAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}
