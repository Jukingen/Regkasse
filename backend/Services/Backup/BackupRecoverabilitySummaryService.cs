using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// <see cref="IBackupRecoverabilitySummaryService"/> — tek kaynak sorgular; restore proof yalnızca Scheduled+Succeeded.
/// </summary>
public sealed class BackupRecoverabilitySummaryService : IBackupRecoverabilitySummaryService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IBackupOperationalReadiness _backupReadiness;

    public BackupRecoverabilitySummaryService(
        AppDbContext db,
        TimeProvider timeProvider,
        IBackupOperationalReadiness backupReadiness)
    {
        _db = db;
        _timeProvider = timeProvider;
        _backupReadiness = backupReadiness;
    }

    /// <inheritdoc />
    public async Task<BackupRecoverabilitySummaryResponseDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var latestBackup = await _db.BackupRuns.AsNoTracking()
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new { r.RequestedAt, r.Status })
            .FirstOrDefaultAsync(cancellationToken);

        var lastSucceededBackup = await _db.BackupRuns.AsNoTracking()
            .Where(r => r.Status == BackupRunStatus.Succeeded)
            .OrderByDescending(r => r.CompletedAt ?? r.RequestedAt)
            .Select(r => new { r.Id, ProofAt = r.CompletedAt ?? r.RequestedAt })
            .FirstOrDefaultAsync(cancellationToken);

        var lastPassedVerification = await _db.BackupVerifications.AsNoTracking()
            .Where(v => v.Status == BackupVerificationStatus.Passed)
            .OrderByDescending(v => v.CompletedAt ?? v.StartedAt)
            .Select(v => new { At = v.CompletedAt ?? v.StartedAt })
            .FirstOrDefaultAsync(cancellationToken);

        // Zamanlanmış drill cadence / DR gözlemi ile hizalı: yalnızca Scheduled başarılı kanıt (manuel başarılar özet yaşını düşürmez).
        var lastRestoreProof = await _db.RestoreVerificationRuns.AsNoTracking()
            .Where(r => r.TriggerSource == RestoreVerificationTriggerSource.Scheduled
                        && r.Status == RestoreVerificationStatus.Succeeded
                        && r.CompletedAt != null)
            .OrderByDescending(r => r.CompletedAt)
            .Select(r => new { r.Id, r.CompletedAt })
            .FirstOrDefaultAsync(cancellationToken);

        var latestRestore = await _db.RestoreVerificationRuns.AsNoTracking()
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new { r.RequestedAt, r.Status })
            .FirstOrDefaultAsync(cancellationToken);

        var cfg = _backupReadiness.GetConfigurationHealth();

        return new BackupRecoverabilitySummaryResponseDto
        {
            LastSuccessfulBackupAt = lastSucceededBackup?.ProofAt,
            LastSuccessfulBackupRunId = lastSucceededBackup?.Id,
            LastSuccessfulArtifactVerificationAt = lastPassedVerification?.At,
            LastSuccessfulRestoreProofAt = lastRestoreProof?.CompletedAt,
            LastSuccessfulRestoreProofRunId = lastRestoreProof?.Id,
            BackupProofAgeSeconds = AgeSeconds(lastSucceededBackup?.ProofAt, nowUtc),
            RestoreProofAgeSeconds = AgeSeconds(lastRestoreProof?.CompletedAt, nowUtc),
            LatestRunAt = latestBackup?.RequestedAt,
            LatestRunStatus = latestBackup?.Status,
            LatestRestoreRunAt = latestRestore?.RequestedAt,
            LatestRestoreRunStatus = latestRestore?.Status,
            BackupExecutionReality = cfg.BackupExecutionReality,
            RealPostgreSqlLogicalDumpConfigured = cfg.RealPostgreSqlLogicalDumpConfigured,
            BackupReadinessLevel = cfg.Level.ToString(),
            BackupReadinessNarrative = cfg.ReadinessNarrative
        };
    }

    private static long? AgeSeconds(DateTime? proofAtUtc, DateTime nowUtc)
    {
        if (!proofAtUtc.HasValue)
            return null;
        var secs = (long)Math.Round((nowUtc - proofAtUtc.Value).TotalSeconds, MidpointRounding.AwayFromZero);
        return secs < 0 ? 0 : secs;
    }
}
