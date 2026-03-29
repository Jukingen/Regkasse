using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.RestoreVerification;

internal static class RestoreVerificationDumpPathResolver
{
    public static async Task<(Guid backupRunId, string absolutePath, string relativeDescriptor)?> TryResolveLatestSucceededLogicalDumpAsync(
        AppDbContext db,
        BackupOptions backupOpts,
        CancellationToken cancellationToken = default)
    {
        var runId = await db.BackupRuns.AsNoTracking()
            .Where(r => r.Status == BackupRunStatus.Succeeded)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (runId == Guid.Empty)
            return null;

        var artifact = await db.BackupArtifacts.AsNoTracking()
            .Where(a => a.BackupRunId == runId && a.ArtifactType == BackupArtifactType.LogicalDump)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (artifact == null)
            return null;

        var stagingRoot = backupOpts.ArtifactStagingRoot;
        if (BackupArtifactPathResolver.TryResolveStagingAbsolute(stagingRoot, artifact.StorageDescriptor, out var abs)
            && File.Exists(abs))
            return (runId, abs, artifact.StorageDescriptor);

        if (!string.IsNullOrWhiteSpace(backupOpts.ExternalArchiveRoot))
        {
            var ext = Path.GetFullPath(backupOpts.ExternalArchiveRoot.Trim());
            var name = Path.GetFileName(artifact.StorageDescriptor);
            if (!string.IsNullOrEmpty(name))
            {
                var archived = Path.GetFullPath(Path.Combine(ext, runId.ToString("N"), name));
                if (File.Exists(archived))
                    return (runId, archived, artifact.StorageDescriptor);
            }
        }

        return null;
    }
}
