using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Forces on-disk SHA-256 re-hash for all artifacts of a run and writes <see cref="BackupVerification"/>.
/// </summary>
public sealed class BackupChecksumVerificationService : IBackupChecksumVerificationService
{
    private readonly AppDbContext _db;
    private readonly IBackupChecksumService _checksum;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<BackupChecksumVerificationService> _logger;

    public BackupChecksumVerificationService(
        AppDbContext db,
        IBackupChecksumService checksum,
        IOptionsMonitor<BackupOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<BackupChecksumVerificationService> logger)
    {
        _db = db;
        _checksum = checksum;
        _options = options;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public Task<BackupChecksumVerifyResponseDto> VerifyChecksumAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        VerifyAndPersistAsync(
            runId,
            IBackupChecksumVerificationService.VerifierSourceOnDemandHttp,
            cancellationToken);

    public async Task<BackupChecksumVerifyResponseDto> VerifyAndPersistAsync(
        Guid backupRunId,
        string verifierSource,
        CancellationToken cancellationToken = default)
    {
        var source = string.IsNullOrWhiteSpace(verifierSource)
            ? IBackupChecksumVerificationService.VerifierSourceOnDemandHttp
            : verifierSource.Trim();
        if (source.Length > 80)
            source = source[..80];

        var startedAt = DateTime.UtcNow;
        var artifacts = await _db.BackupArtifacts
            .AsNoTracking()
            .Where(a => a.BackupRunId == backupRunId)
            .OrderBy(a => a.ArtifactType)
            .ThenBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = new List<BackupChecksumArtifactResultDto>(artifacts.Count);
        string? failureReason = null;
        var allValid = true;

        if (artifacts.Count == 0)
        {
            allValid = false;
            failureReason = "No artifacts produced; nothing to verify.";
        }
        else
        {
            var opts = _options.CurrentValue;
            foreach (var artifact in artifacts)
            {
                var row = await VerifyOneArtifactAsync(backupRunId, artifact, opts, cancellationToken)
                    .ConfigureAwait(false);
                results.Add(row);
                if (!string.Equals(row.Status, "passed", StringComparison.Ordinal))
                {
                    allValid = false;
                    failureReason ??= row.Detail ?? $"Artifact {row.ArtifactType} checksum verification failed ({row.Status}).";
                }
            }
        }

        var completedAt = DateTime.UtcNow;
        var verification = new BackupVerification
        {
            Id = Guid.NewGuid(),
            BackupRunId = backupRunId,
            Status = allValid ? BackupVerificationStatus.Passed : BackupVerificationStatus.Failed,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            VerifierSource = source,
            CompletenessFlag = artifacts.Any(a => a.ArtifactType == BackupArtifactType.LogicalDump),
            FailureReason = allValid ? null : Truncate(failureReason, 4000),
            DetailsJson = JsonSerializer.Serialize(new
            {
                backupRunId,
                verifierSource = source,
                isValid = allValid,
                artifacts = results.Select(r => new
                {
                    r.ArtifactType,
                    r.Status,
                    stored = r.StoredChecksum,
                    computed = r.ComputedChecksum,
                    r.Detail
                })
            })
        };

        _db.BackupVerifications.Add(verification);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Checksum verification {Source} for run {BackupRunId}: valid={IsValid} verificationId={VerificationId}",
            source,
            backupRunId,
            allValid,
            verification.Id);

        return new BackupChecksumVerifyResponseDto
        {
            RunId = backupRunId,
            IsValid = allValid,
            VerifiedAtUtc = completedAt,
            VerifierSource = source,
            VerificationId = verification.Id,
            FailureReason = allValid ? null : failureReason,
            Artifacts = results
        };
    }

    private async Task<BackupChecksumArtifactResultDto> VerifyOneArtifactAsync(
        Guid backupRunId,
        BackupArtifact artifact,
        BackupOptions opts,
        CancellationToken cancellationToken)
    {
        var typeName = artifact.ArtifactType.ToString();
        var stored = artifact.ContentHashSha256?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(stored) || stored.Length != 64)
        {
            return new BackupChecksumArtifactResultDto
            {
                ArtifactType = typeName,
                StoredChecksum = artifact.ContentHashSha256,
                Status = "missing_hash",
                Detail = $"Artifact {typeName} missing valid SHA-256."
            };
        }

        if (!BackupArtifactOnDiskResolver.TryResolveForSingleRun(
                backupRunId,
                artifact,
                opts,
                _logger,
                _hostEnvironment,
                "ChecksumVerify",
                out var absolutePath)
            || string.IsNullOrWhiteSpace(absolutePath))
        {
            var redacted = BackupArtifactPublicFormatter.RedactedStagingLocator(
                artifact.ArtifactType,
                artifact.StorageDescriptor);
            return new BackupChecksumArtifactResultDto
            {
                ArtifactType = typeName,
                StoredChecksum = stored,
                Status = "missing_file",
                Detail = $"Artifact file missing on disk/archive: {typeName} ({redacted})."
            };
        }

        var computed = await _checksum.ComputeFileSha256HexAsync(absolutePath, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(computed, stored, StringComparison.Ordinal))
        {
            return new BackupChecksumArtifactResultDto
            {
                ArtifactType = typeName,
                StoredChecksum = stored,
                ComputedChecksum = computed,
                Status = "failed",
                Detail = $"On-disk SHA-256 mismatch for {typeName}."
            };
        }

        return new BackupChecksumArtifactResultDto
        {
            ArtifactType = typeName,
            StoredChecksum = stored,
            ComputedChecksum = computed,
            Status = "passed"
        };
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
            return value;
        return value[..max];
    }
}
