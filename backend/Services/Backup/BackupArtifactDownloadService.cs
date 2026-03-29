using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupArtifactDownloadService : IBackupArtifactDownloadService
{
    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly ILogger<BackupArtifactDownloadService> _logger;

    public BackupArtifactDownloadService(
        AppDbContext db,
        IOptionsMonitor<BackupOptions> options,
        ILogger<BackupArtifactDownloadService> logger)
    {
        _db = db;
        _options = options;
        _logger = logger;
    }

    public async Task<BackupArtifactDownloadPrepareResult> PrepareDownloadAsync(
        Guid backupRunId,
        Guid artifactId,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.BackupRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == backupRunId, cancellationToken);
        if (run == null)
            return BackupArtifactDownloadPrepareResult.Fail(BackupArtifactDownloadPrepareStatus.RunNotFound);

        if (run.Status != BackupRunStatus.Succeeded)
            return BackupArtifactDownloadPrepareResult.Fail(BackupArtifactDownloadPrepareStatus.RunNotSucceeded);

        var artifact = await _db.BackupArtifacts.AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == artifactId && a.BackupRunId == backupRunId,
                cancellationToken);
        if (artifact == null)
            return BackupArtifactDownloadPrepareResult.Fail(BackupArtifactDownloadPrepareStatus.ArtifactNotFound);

        var opts = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(opts.ArtifactStagingRoot)
            && string.IsNullOrWhiteSpace(opts.ExternalArchiveRoot))
            return BackupArtifactDownloadPrepareResult.Fail(BackupArtifactDownloadPrepareStatus.InvalidConfiguration);

        if (!BackupArtifactOnDiskResolver.TryResolveForSingleRun(
                backupRunId,
                artifact,
                opts,
                _logger,
                "Backup artifact download",
                out var absolutePath))
            return BackupArtifactDownloadPrepareResult.Fail(BackupArtifactDownloadPrepareStatus.FileNotOnDisk);

        var downloadName = BuildSafeDownloadFileName(artifact);
        return BackupArtifactDownloadPrepareResult.OkResult(absolutePath, downloadName);
    }

    private static string BuildSafeDownloadFileName(BackupArtifact artifact)
    {
        var raw = Path.GetFileName(artifact.StorageDescriptor.Trim());
        if (string.IsNullOrEmpty(raw))
            raw = $"{artifact.ArtifactType}_{artifact.Id:N}";

        foreach (var c in Path.GetInvalidFileNameChars())
            raw = raw.Replace(c, '_');

        if (string.IsNullOrWhiteSpace(raw))
            raw = $"{artifact.ArtifactType}_{artifact.Id:N}";

        return raw;
    }
}
