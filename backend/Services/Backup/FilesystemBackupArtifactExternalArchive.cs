using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Copies files from staging into ExternalArchiveRoot/{runId:N}/ with post-copy SHA-256 verification.
/// </summary>
public sealed class FilesystemBackupArtifactExternalArchive : IBackupArtifactExternalArchive
{
    private readonly IBackupChecksumService _checksum;
    private readonly ILogger<FilesystemBackupArtifactExternalArchive> _logger;

    public FilesystemBackupArtifactExternalArchive(
        IBackupChecksumService checksum,
        ILogger<FilesystemBackupArtifactExternalArchive> logger)
    {
        _checksum = checksum;
        _logger = logger;
    }

    public async Task<BackupExternalArchiveOutcome> CopyStagingArtifactsAsync(
        Guid backupRunId,
        string stagingRootFull,
        string externalArchiveRootFull,
        IReadOnlyList<BackupArtifactDescriptor> artifacts,
        CancellationToken cancellationToken = default)
    {
        var extRoot = Path.GetFullPath(externalArchiveRootFull.Trim());

        if (File.Exists(extRoot) && (File.GetAttributes(extRoot) & FileAttributes.Directory) == 0)
        {
            return new BackupExternalArchiveOutcome
            {
                Success = false,
                ErrorCode = "ARCHIVE_ROOT_NOT_A_DIRECTORY",
                ErrorDetail = "ExternalArchiveRoot points to a file, not a directory."
            };
        }

        try
        {
            Directory.CreateDirectory(extRoot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot create external archive root: runId={RunId}", backupRunId);
            return new BackupExternalArchiveOutcome
            {
                Success = false,
                ErrorCode = "ARCHIVE_ROOT_CREATE_FAILED",
                ErrorDetail = "External archive root could not be created."
            };
        }

        var probePath = Path.Combine(extRoot, ".regkasse_archive_write_probe");
        try
        {
            await File.WriteAllTextAsync(probePath, "ok", cancellationToken);
            File.Delete(probePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External archive root not writable: runId={RunId}", backupRunId);
            return new BackupExternalArchiveOutcome
            {
                Success = false,
                ErrorCode = "ARCHIVE_ROOT_NOT_WRITABLE",
                ErrorDetail = "External archive root is not writable (probe failed)."
            };
        }

        var destDir = Path.GetFullPath(Path.Combine(extRoot, backupRunId.ToString("N")));
        if (!BackupPathGuard.IsPathUnderRoot(destDir, extRoot))
        {
            return new BackupExternalArchiveOutcome
            {
                Success = false,
                ErrorCode = "ARCHIVE_PATH_ESCAPE",
                ErrorDetail = "Resolved external archive run directory left ExternalArchiveRoot."
            };
        }

        Directory.CreateDirectory(destDir);
        var locators = new Dictionary<BackupArtifactType, string>();

        foreach (var a in artifacts)
        {
            if (!BackupArtifactPathResolver.TryResolveStagingAbsolute(stagingRootFull, a.StorageDescriptor, out var src))
            {
                return new BackupExternalArchiveOutcome
                {
                    Success = false,
                    ErrorCode = "STAGING_PATH_RESOLVE",
                    ErrorDetail = $"Cannot resolve staging path for {a.ArtifactType}."
                };
            }

            if (!File.Exists(src))
            {
                return new BackupExternalArchiveOutcome
                {
                    Success = false,
                    ErrorCode = "SOURCE_MISSING",
                    ErrorDetail = $"Staging file missing for {a.ArtifactType}."
                };
            }

            var fileName = Path.GetFileName(src);
            if (string.IsNullOrEmpty(fileName))
            {
                return new BackupExternalArchiveOutcome
                {
                    Success = false,
                    ErrorCode = "INVALID_FILE_NAME",
                    ErrorDetail = $"No file name for artifact {a.ArtifactType}."
                };
            }

            var destPath = Path.GetFullPath(Path.Combine(destDir, fileName));
            if (!BackupPathGuard.IsPathUnderRoot(destPath, destDir))
            {
                return new BackupExternalArchiveOutcome
                {
                    Success = false,
                    ErrorCode = "DEST_PATH_ESCAPE",
                    ErrorDetail = "Destination path escaped run directory."
                };
            }

            File.Copy(src, destPath, overwrite: true);

            if (string.IsNullOrWhiteSpace(a.ContentHashSha256))
            {
                return new BackupExternalArchiveOutcome
                {
                    Success = false,
                    ErrorCode = "MISSING_EXPECTED_HASH",
                    ErrorDetail = $"No expected hash for {a.ArtifactType}."
                };
            }

            var ok = await _checksum.FileMatchesSha256Async(destPath, a.ContentHashSha256, cancellationToken);
            if (!ok)
            {
                _logger.LogWarning(
                    "External archive post-copy SHA-256 mismatch: runId={RunId}, artifactType={Type}",
                    backupRunId,
                    a.ArtifactType);
                return new BackupExternalArchiveOutcome
                {
                    Success = false,
                    ErrorCode = "POST_COPY_HASH_MISMATCH",
                    ErrorDetail = $"Post-copy SHA-256 mismatch for {a.ArtifactType}."
                };
            }

            var redacted = $"archive/{backupRunId:N}/{fileName}";
            locators[a.ArtifactType] = redacted;
        }

        _logger.LogInformation(
            "External archive copy completed with post-copy hash verification: runId={RunId}, fileCount={Count}",
            backupRunId,
            artifacts.Count);

        return new BackupExternalArchiveOutcome { Success = true, RedactedLocators = locators };
    }
}
