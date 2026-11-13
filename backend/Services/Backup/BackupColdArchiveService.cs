using System.IO.Compression;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Compresses (when useful), deduplicates by SHA-256, and uploads artifacts to the configured cold backend.
/// </summary>
public sealed class BackupColdArchiveService : IBackupColdArchiveService
{
    private readonly AppDbContext _db;
    private readonly ICloudStorageService _cloud;
    private readonly IBackupRetentionPolicyService _policy;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IAuditLogService _audit;
    private readonly ILogger<BackupColdArchiveService> _logger;

    public BackupColdArchiveService(
        AppDbContext db,
        ICloudStorageService cloud,
        IBackupRetentionPolicyService policy,
        IOptionsMonitor<BackupOptions> options,
        IHostEnvironment hostEnvironment,
        IAuditLogService audit,
        ILogger<BackupColdArchiveService> logger)
    {
        _db = db;
        _cloud = cloud;
        _policy = policy;
        _options = options;
        _hostEnvironment = hostEnvironment;
        _audit = audit;
        _logger = logger;
    }

    public async Task<BackupMoveToColdResponseDto> MoveRunToColdAsync(
        BackupRun run,
        string? actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        var snap = await _policy.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (!snap.ColdStorageEnabled)
        {
            return new BackupMoveToColdResponseDto
            {
                RunId = run.Id,
                Success = false,
                Message = "Cold storage is disabled in the retention policy."
            };
        }

        if (!_cloud.IsConfigured)
        {
            return new BackupMoveToColdResponseDto
            {
                RunId = run.Id,
                Success = false,
                Message = "Cold storage provider is not configured."
            };
        }

        var artifacts = run.Artifacts.Count > 0
            ? run.Artifacts.ToList()
            : await _db.BackupArtifacts.Where(a => a.BackupRunId == run.Id).ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var archived = 0;
        var deduped = 0;
        var years = Math.Max(snap.ColdRetentionYears, BackupRetentionPolicySettings.MinColdRetentionYears);
        var immutableUntil = (run.CompletedAt ?? run.RequestedAt).AddYears(years);

        foreach (var artifact in artifacts)
        {
            if (!string.IsNullOrWhiteSpace(artifact.CloudLocator) && artifact.MovedToColdAtUtc != null)
                continue;

            if (artifact.ContentHashSha256 is { Length: > 0 } hash)
            {
                var existing = await _db.BackupArtifacts.AsNoTracking()
                    .Where(a => a.Id != artifact.Id
                                && a.ContentHashSha256 == hash
                                && a.CloudLocator != null)
                    .OrderByDescending(a => a.MovedToColdAtUtc)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (existing != null)
                {
                    artifact.CloudLocator = existing.CloudLocator;
                    artifact.CloudProvider = existing.CloudProvider;
                    artifact.DeduplicatedFromArtifactId = existing.Id;
                    artifact.CompressedByteSize = existing.CompressedByteSize;
                    artifact.MovedToColdAtUtc = DateTime.UtcNow;
                    artifact.ImmutableUntilUtc = immutableUntil;
                    artifact.StorageTier = BackupStorageTier.Cold;
                    deduped++;
                    continue;
                }
            }

            if (!TryReadBytes(run, artifact, out var bytes) || bytes.Length == 0)
            {
                _logger.LogWarning(
                    "Cold archive skipped missing local file: runId={RunId} artifactId={ArtifactId}",
                    run.Id,
                    artifact.Id);
                continue;
            }

            var payload = MaybeCompress(bytes, artifact);
            var objectKey = $"{run.Id:N}/{artifact.Id:N}/{SanitizeFileName(artifact.StorageDescriptor)}";
            if (payload.Compressed)
                objectKey += ".gz";

            var upload = await _cloud.UploadAsync(
                    new CloudStorageUploadRequest
                    {
                        ObjectKey = objectKey,
                        Content = payload.Bytes,
                        ContentSha256Hex = artifact.ContentHashSha256,
                        ImmutableUntilUtc = immutableUntil,
                        ContentType = payload.Compressed ? "application/gzip" : "application/octet-stream"
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (!upload.Success)
            {
                return new BackupMoveToColdResponseDto
                {
                    RunId = run.Id,
                    Success = false,
                    ArtifactsArchived = archived,
                    ArtifactsDeduplicated = deduped,
                    Provider = _cloud.ResolvedProvider.ToString(),
                    Message = upload.Error ?? "Cloud upload failed."
                };
            }

            artifact.CloudLocator = upload.RedactedLocator;
            artifact.CloudProvider = _cloud.ResolvedProvider.ToString();
            artifact.CompressedByteSize = payload.Bytes.LongLength;
            artifact.MovedToColdAtUtc = DateTime.UtcNow;
            artifact.ImmutableUntilUtc = immutableUntil;
            artifact.StorageTier = BackupStorageTier.Cold;
            if (string.IsNullOrWhiteSpace(artifact.ExternalRedactedLocator))
                artifact.ExternalRedactedLocator = upload.RedactedLocator;
            archived++;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.LogSystemOperationAsync(
            action: "BACKUP_MOVED_TO_COLD",
            entityType: "BackupRun",
            userId: actorUserId ?? "system",
            userRole: actorRole,
            description: $"Backup run moved to cold storage ({archived} uploaded, {deduped} deduplicated).",
            status: AuditLogStatus.Success,
            actionType: AuditEventType.BackupMovedToColdStorage,
            entityId: run.Id,
            responseData: new
            {
                run.Id,
                archived,
                deduped,
                provider = _cloud.ResolvedProvider.ToString()
            }).ConfigureAwait(false);

        return new BackupMoveToColdResponseDto
        {
            RunId = run.Id,
            Success = true,
            ArtifactsArchived = archived,
            ArtifactsDeduplicated = deduped,
            Provider = _cloud.ResolvedProvider.ToString(),
            Message = archived == 0 && deduped == 0
                ? "No artifacts required upload (already in cold storage or files missing)."
                : null
        };
    }

    public async Task<int> ArchiveAgedSucceededRunsAsync(CancellationToken cancellationToken = default)
    {
        var snap = await _policy.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (!snap.ColdStorageEnabled || !_cloud.IsConfigured)
            return 0;

        var cutoff = DateTime.UtcNow.AddDays(-snap.WarmRetentionDays);
        var runs = await _db.BackupRuns
            .Include(r => r.Artifacts)
            .Where(r => r.Status == BackupRunStatus.Succeeded && r.CompletedAt != null && r.CompletedAt < cutoff)
            .Where(r => r.Artifacts.Any(a => a.CloudLocator == null && a.MovedToColdAtUtc == null))
            .OrderBy(r => r.CompletedAt)
            .Take(25)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var moved = 0;
        foreach (var run in runs)
        {
            var result = await MoveRunToColdAsync(run, "system", "System", cancellationToken).ConfigureAwait(false);
            if (result.Success && (result.ArtifactsArchived > 0 || result.ArtifactsDeduplicated > 0))
                moved++;
        }

        return moved;
    }

    private bool TryReadBytes(BackupRun run, BackupArtifact artifact, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (!BackupArtifactOnDiskResolver.TryResolveForSingleRun(
                run.Id,
                artifact,
                _options.CurrentValue,
                _logger,
                _hostEnvironment,
                "backup_cold_archive",
                out var absolute)
            || !File.Exists(absolute))
        {
            return false;
        }

        bytes = File.ReadAllBytes(absolute);
        return true;
    }

    private static (byte[] Bytes, bool Compressed) MaybeCompress(byte[] source, BackupArtifact artifact)
    {
        var name = (artifact.StorageDescriptor ?? string.Empty).ToLowerInvariant();
        if (name.EndsWith(".gz", StringComparison.Ordinal)
            || name.EndsWith(".zip", StringComparison.Ordinal)
            || name.EndsWith(".dump", StringComparison.Ordinal)
            || source.Length < 64 * 1024)
        {
            return (source, false);
        }

        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(source, 0, source.Length);
        var compressed = output.ToArray();
        return compressed.Length < source.Length ? (compressed, true) : (source, false);
    }

    private static string SanitizeFileName(string? descriptor)
    {
        var name = Path.GetFileName(descriptor ?? "artifact.bin");
        if (string.IsNullOrWhiteSpace(name))
            name = "artifact.bin";
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
