using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

public static class BackupRetentionStatusEvaluator
{
    public static BackupRetentionStatusDto FromRun(
        BackupRun run,
        BackupRetentionPolicySnapshot? policy = null,
        DateTime? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        var now = utcNow ?? DateTime.UtcNow;
        var snap = policy ?? BackupRetentionPolicySnapshot.Defaults;
        var dump = run.Artifacts
            .Where(a => a.ArtifactType == BackupArtifactType.LogicalDump)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault()
            ?? run.Artifacts.OrderByDescending(a => a.CreatedAt).FirstOrDefault();

        var tier = dump?.StorageTier ?? BackupStorageTier.Hot;
        var inCold = dump is { CloudLocator: not null } || dump?.MovedToColdAtUtc != null
                     || tier == BackupStorageTier.Cold;
        var expires = BackupRetentionGuard.ResolveRetentionExpiresAtUtc(run, snap);
        var canDelete = BackupRetentionGuard.CanDeleteSucceededRun(run, snap, now)
                        && run.Status == BackupRunStatus.Succeeded
                        && expires is DateTime exp
                        && exp <= now;

        string status;
        if (run.LegalHold || (run.LegalHoldUntilUtc is DateTime hold && hold > now))
            status = "legal_hold";
        else if (inCold)
            status = "cold";
        else
            status = tier switch
            {
                BackupStorageTier.Warm => "warm",
                BackupStorageTier.Cold => "cold",
                _ => "hot"
            };

        return new BackupRetentionStatusDto
        {
            RunId = run.Id,
            StorageTier = tier,
            LegalHold = run.LegalHold,
            LegalHoldUntilUtc = run.LegalHoldUntilUtc,
            LegalHoldReason = run.LegalHoldReason,
            RetentionExpiresAtUtc = expires,
            Status = status,
            CanDelete = canDelete,
            InColdStorage = inCold,
            CloudLocator = dump?.CloudLocator
        };
    }
}
