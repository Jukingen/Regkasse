using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

public interface IBackupRunService
{
    /// <summary>
    /// Loads a backup run with artifacts and verifications and maps to <see cref="BackupRunResponseDto"/>
    /// including computed size, duration, and optional original-database size estimate.
    /// </summary>
    Task<BackupRunResponseDto?> GetBackupRunAsync(
        Guid runId,
        BackupRunDtoMappingOptions mappingOptions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Readable list of succeeded logical dump artifacts (file name, tenant slug, download path).
    /// </summary>
    Task<IReadOnlyList<BackupListItemResponseDto>> GetBackupListAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>Inputs required to map a <see cref="Models.Backup.BackupRun"/> to admin API DTOs.</summary>
public sealed class BackupRunDtoMappingOptions
{
    public BackupArtifactPipelinePolicySnapshot? PipelinePolicy { get; init; }

    public int? AutomaticRetryMaxAttemptsBudget { get; init; }

    public BackupDownloadEnrichment? DownloadEnrichment { get; init; }
}
