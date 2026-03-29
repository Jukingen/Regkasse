using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.Backup;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Phase 1–3: artifact verification (metadata + optional on-disk SHA-256). Never restore verification / pg_verifybackup.
/// </summary>
public sealed class BackupVerificationService : IBackupVerificationService
{
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IBackupChecksumService _checksum;

    public BackupVerificationService(IOptionsMonitor<BackupOptions> options, IBackupChecksumService checksum)
    {
        _options = options;
        _checksum = checksum;
    }

    public async Task<BackupVerificationOutcome> VerifyArtifactsAsync(
        Guid backupRunId,
        IReadOnlyList<BackupArtifactDescriptor> artifacts,
        CancellationToken cancellationToken = default)
    {
        if (_options.CurrentValue.DevelopmentForceVerificationFailure)
        {
            return new BackupVerificationOutcome(
                false,
                false,
                "DevelopmentForceVerificationFailure=true",
                JsonSerializer.Serialize(new { backupRunId, reason = "forced_failure" }));
        }

        if (artifacts.Count == 0)
        {
            return new BackupVerificationOutcome(
                false,
                false,
                "No artifacts produced; nothing to verify.",
                JsonSerializer.Serialize(new { backupRunId }));
        }

        foreach (var a in artifacts)
        {
            if (string.IsNullOrWhiteSpace(a.ContentHashSha256) || a.ContentHashSha256.Length != 64)
            {
                return new BackupVerificationOutcome(
                    false,
                    false,
                    $"Artifact {a.ArtifactType} missing valid SHA-256.",
                    JsonSerializer.Serialize(new { backupRunId, a.ArtifactType }));
            }
        }

        var root = _options.CurrentValue.ArtifactStagingRoot;
        var rootFull = string.IsNullOrWhiteSpace(root) ? null : Path.GetFullPath(root.Trim());
        var diskVerify = _options.CurrentValue.VerifyLogicalDumpFileOnDisk;
        var usedOnDisk = false;

        foreach (var a in artifacts)
        {
            if (!diskVerify || !a.RequireOnDiskHashVerification)
                continue;

            if (rootFull == null)
            {
                return new BackupVerificationOutcome(
                    false,
                    false,
                    "On-disk verification requested but Backup:ArtifactStagingRoot is not set.",
                    JsonSerializer.Serialize(new { backupRunId, a.ArtifactType }));
            }

            if (!BackupArtifactPathResolver.TryResolveStagingAbsolute(rootFull, a.StorageDescriptor, out var full))
            {
                var redacted = BackupArtifactPublicFormatter.RedactedStagingLocator(a.ArtifactType, a.StorageDescriptor);
                return new BackupVerificationOutcome(
                    false,
                    false,
                    $"Artifact {a.ArtifactType} path is not under staging root.",
                    JsonSerializer.Serialize(new { backupRunId, a.ArtifactType, locator = redacted }));
            }

            if (!File.Exists(full))
            {
                var redacted = BackupArtifactPublicFormatter.RedactedStagingLocator(a.ArtifactType, a.StorageDescriptor);
                return new BackupVerificationOutcome(
                    false,
                    false,
                    $"Artifact file missing on disk: {a.ArtifactType}.",
                    JsonSerializer.Serialize(new { backupRunId, a.ArtifactType, locator = redacted }));
            }

            var hex = await _checksum.ComputeFileSha256HexAsync(full, cancellationToken);
            if (!string.Equals(hex, a.ContentHashSha256, StringComparison.Ordinal))
            {
                var redacted = BackupArtifactPublicFormatter.RedactedStagingLocator(a.ArtifactType, a.StorageDescriptor);
                return new BackupVerificationOutcome(
                    false,
                    false,
                    $"On-disk SHA-256 mismatch for {a.ArtifactType}.",
                    JsonSerializer.Serialize(new
                    {
                        backupRunId,
                        a.ArtifactType,
                        locator = redacted,
                        expected = a.ContentHashSha256,
                        actual = hex
                    }));
            }

            usedOnDisk = true;
        }

        var hasLogical = artifacts.Any(x => x.ArtifactType == BackupArtifactType.LogicalDump);
        var completeness = hasLogical;

        var kind = usedOnDisk ? "ArtifactMetadataAndOnDiskSha256" : "ArtifactMetadataSha256";

        return new BackupVerificationOutcome(
            true,
            completeness,
            null,
            JsonSerializer.Serialize(new
            {
                backupRunId,
                artifactCount = artifacts.Count,
                completeness,
                artifactVerificationKind = kind,
                scope = "not_restore_verification",
                onDiskVerificationUsed = usedOnDisk
            }));
    }
}
