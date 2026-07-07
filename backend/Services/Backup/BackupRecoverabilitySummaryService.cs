using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Backup;
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
    public async Task<BackupRecoverabilitySummaryResponseDto> GetAsync(
        BackupRunAccessScope? accessScope = null,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var latestBackup = await AccessibleRuns(accessScope)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new { r.RequestedAt, r.Status })
            .FirstOrDefaultAsync(cancellationToken);

        var lastSucceededBackup = await AccessibleRuns(accessScope)
            .Where(r => r.Status == BackupRunStatus.Succeeded)
            .OrderByDescending(r => r.CompletedAt ?? r.RequestedAt)
            .Select(r => new { r.Id, ProofAt = r.CompletedAt ?? r.RequestedAt, r.AdapterKind })
            .FirstOrDefaultAsync(cancellationToken);

        var lastPassedVerificationAt = await LastPassedVerificationAtAsync(accessScope, cancellationToken);

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

        bool? lastSuccessSimulated = null;
        if (lastSucceededBackup != null)
        {
            lastSuccessSimulated = BackupCompletenessSuccessPolicy.TryParseAdapterKind(
                    lastSucceededBackup.AdapterKind,
                    out var ak)
                && (ak == BackupExecutionAdapterKind.Fake || ak == BackupExecutionAdapterKind.ProductionStub);
        }

        return new BackupRecoverabilitySummaryResponseDto
        {
            LastSuccessfulBackupAt = lastSucceededBackup?.ProofAt,
            LastSuccessfulBackupRunId = lastSucceededBackup?.Id,
            LastSuccessfulBackupRunIsSimulatedExecution = lastSuccessSimulated,
            LastSuccessfulArtifactVerificationAt = lastPassedVerificationAt,
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

    private IQueryable<BackupRun> AccessibleRuns(BackupRunAccessScope? accessScope)
    {
        var q = _db.BackupRuns.AsNoTracking();
        return accessScope == null
            ? q
            : BackupRunAccessEvaluator.ApplyCallerAccessFilter(q, accessScope);
    }

    private async Task<DateTime?> LastPassedVerificationAtAsync(
        BackupRunAccessScope? accessScope,
        CancellationToken cancellationToken)
    {
        var accessibleRunIds = AccessibleRuns(accessScope);
        return await _db.BackupVerifications.AsNoTracking()
            .Where(v => v.Status == BackupVerificationStatus.Passed
                        && accessibleRunIds.Select(r => r.Id).Contains(v.BackupRunId))
            .OrderByDescending(v => v.CompletedAt ?? v.StartedAt)
            .Select(v => (DateTime?)(v.CompletedAt ?? v.StartedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static long? AgeSeconds(DateTime? proofAtUtc, DateTime nowUtc)
    {
        if (!proofAtUtc.HasValue)
            return null;
        var secs = (long)Math.Round((nowUtc - proofAtUtc.Value).TotalSeconds, MidpointRounding.AwayFromZero);
        return secs < 0 ? 0 : secs;
    }
}
