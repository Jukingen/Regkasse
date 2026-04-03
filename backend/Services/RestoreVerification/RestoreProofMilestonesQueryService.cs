using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class RestoreProofMilestonesQueryService : IRestoreProofMilestonesQueryService
{
    private readonly AppDbContext _db;
    private static readonly string PgDumpAdapter = nameof(BackupExecutionAdapterKind.PgDump);

    public RestoreProofMilestonesQueryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<RestoreProofMilestonesResponseDto> GetMilestonesAsync(CancellationToken cancellationToken = default)
    {
        var latestBackup = await _db.BackupRuns.AsNoTracking()
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var latestPgDumpOk = await _db.BackupRuns.AsNoTracking()
            .Where(r => r.Status == BackupRunStatus.Succeeded && r.AdapterKind == PgDumpAdapter)
            .OrderByDescending(r => r.CompletedAt ?? r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var latestArtifact = await (
            from a in _db.BackupArtifacts.AsNoTracking()
            join br in _db.BackupRuns.AsNoTracking() on a.BackupRunId equals br.Id
            where br.Status == BackupRunStatus.Succeeded && br.AdapterKind == PgDumpAdapter
            orderby a.CreatedAt descending
            select a).FirstOrDefaultAsync(cancellationToken);

        var latestDrill = await _db.RestoreVerificationRuns.AsNoTracking()
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var latestDrillSucceeded = await _db.RestoreVerificationRuns.AsNoTracking()
            .Where(r => r.Status == RestoreVerificationStatus.Succeeded)
            .OrderByDescending(r => r.CompletedAt ?? r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lkgL4 = await _db.RestoreVerificationRuns.AsNoTracking()
            .Where(r => r.Status == RestoreVerificationStatus.Succeeded
                        && r.PostRestoreContinuityChecksExecuted
                        && r.PostRestoreContinuityChecksPassed == true)
            .OrderByDescending(r => r.CompletedAt ?? r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lkgL5Http = await _db.RestoreVerificationRuns.AsNoTracking()
            .Where(r => r.Status == RestoreVerificationStatus.Succeeded
                        && r.ApplicationSmokeProbeExecuted
                        && r.ApplicationSmokeProbePassed == true)
            .OrderByDescending(r => r.CompletedAt ?? r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lkgL5a = await _db.RestoreVerificationRuns.AsNoTracking()
            .Where(r => r.Status == RestoreVerificationStatus.Succeeded
                        && r.RestoredDatabaseApplicationSmokeExecuted
                        && r.RestoredDatabaseApplicationSmokePassed == true)
            .OrderByDescending(r => r.CompletedAt ?? r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var warnings = BuildWarningCodes(latestDrill, latestDrillSucceeded, lkgL4);

        return new RestoreProofMilestonesResponseDto
        {
            LatestBackupRun = MapBackup(latestBackup, RestoreProofMilestoneKind.LatestBackupRun),
            LatestPgDumpSucceededBackupRun = MapBackup(latestPgDumpOk, RestoreProofMilestoneKind.LatestPgDumpSucceededBackupRun),
            LatestPgDumpSucceededArtifact = MapArtifact(latestArtifact),
            LatestRestoreDrillAttempt = MapDrill(latestDrill, RestoreProofMilestoneKind.LatestRestoreDrillAttempt),
            LatestRestoreDrillSucceeded = MapDrill(latestDrillSucceeded, RestoreProofMilestoneKind.LatestRestoreDrillSucceeded),
            LastKnownGoodL4ContinuityProven = MapDrill(lkgL4, RestoreProofMilestoneKind.LastKnownGoodL4ContinuityProven),
            LastKnownGoodL5HttpSmokeProven = MapDrill(lkgL5Http, RestoreProofMilestoneKind.LastKnownGoodL5HttpSmokeProven),
            LastKnownGoodL5aRestoredDbSmokeProven = MapDrill(lkgL5a, RestoreProofMilestoneKind.LastKnownGoodL5aRestoredDbSmokeProven),
            Semantics = new RestoreProofMilestonesSemanticsDto { WarningCodes = warnings }
        };
    }

    private static IReadOnlyList<string> BuildWarningCodes(
        RestoreVerificationRun? latestDrill,
        RestoreVerificationRun? latestSucceeded,
        RestoreVerificationRun? lkgL4)
    {
        var w = new List<string>();
        if (latestDrill?.Status == RestoreVerificationStatus.Failed)
            w.Add("LATEST_DRILL_ATTEMPT_FAILED");

        if (latestSucceeded != null && lkgL4 != null
            && latestSucceeded.Id != lkgL4.Id
            && AsOf(latestSucceeded) > AsOf(lkgL4)
            && !RestoreProofMilestoneSelectors.IsL4ContinuityProven(latestSucceeded))
            w.Add("NEWER_DRILL_SUCCESS_WITHOUT_L4_CONTINUITY_PROOF");

        return w;
    }

    private static DateTime AsOf(RestoreVerificationRun r) =>
        r.CompletedAt ?? r.StartedAt ?? r.RequestedAt;

    private static RestoreProofMilestoneSnapshotDto? MapBackup(BackupRun? r, RestoreProofMilestoneKind kind)
    {
        if (r == null)
            return null;
        return new RestoreProofMilestoneSnapshotDto
        {
            Kind = kind,
            EntityType = "backup_run",
            Id = r.Id,
            AsOfUtc = r.CompletedAt ?? r.StartedAt ?? r.RequestedAt,
            DrillStatus = null,
            SourceBackupRunId = null,
            SourceBackupArtifactId = null
        };
    }

    private static RestoreProofMilestoneSnapshotDto? MapArtifact(BackupArtifact? a)
    {
        if (a == null)
            return null;
        return new RestoreProofMilestoneSnapshotDto
        {
            Kind = RestoreProofMilestoneKind.LatestPgDumpSucceededArtifact,
            EntityType = "backup_artifact",
            Id = a.Id,
            AsOfUtc = a.CreatedAt,
            DrillStatus = null,
            SourceBackupRunId = a.BackupRunId,
            SourceBackupArtifactId = a.Id
        };
    }

    private static RestoreProofMilestoneSnapshotDto? MapDrill(RestoreVerificationRun? r, RestoreProofMilestoneKind kind)
    {
        if (r == null)
            return null;
        return new RestoreProofMilestoneSnapshotDto
        {
            Kind = kind,
            EntityType = "restore_verification_run",
            Id = r.Id,
            AsOfUtc = RestoreProofMilestoneSelectors.MilestoneAsOfUtc(r),
            DrillStatus = r.Status,
            SourceBackupRunId = r.SourceBackupRunId,
            SourceBackupArtifactId = r.SourceBackupArtifactId
        };
    }
}
