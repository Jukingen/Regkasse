using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Point-in-time recovery planning based on succeeded backup runs and declared WAL archiving (Phase 2+ automation deferred).
/// </summary>
public sealed class PitrService : IPitrService
{
    public const string RecoveryMethodPitr = "PITR";
    public const string RecoveryMethodFullBackupOnly = "FullBackupOnly";

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<BackupOptions> _backupOptions;
    private readonly TimeProvider _time;
    private readonly ILogger<PitrService> _logger;

    public PitrService(
        AppDbContext db,
        IOptionsMonitor<BackupOptions> backupOptions,
        TimeProvider time,
        ILogger<PitrService> logger)
    {
        _db = db;
        _backupOptions = backupOptions;
        _time = time;
        _logger = logger;
    }

    public async Task<PitrAvailabilityResponseDto> GetPitrAvailabilityAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var backups = await LoadSucceededBackupsAsync(tenantId, cancellationToken);
        if (backups.Count == 0)
        {
            return new PitrAvailabilityResponseDto
            {
                IsAvailable = false,
                TenantIdFilter = tenantId,
                Message = tenantId.HasValue
                    ? "No successful backups found for the tenant filter hint (manual idempotency key)."
                    : "No successful backups found."
            };
        }

        var walStatus = await GetWalArchiveStatusAsync(cancellationToken);
        var completed = backups
            .Where(b => b.CompletedAt.HasValue)
            .Select(b => b.CompletedAt!.Value)
            .ToList();

        var earliest = completed.Min();
        var latestBackup = completed.Max();
        var now = _time.GetUtcNow().UtcDateTime;
        var latest = latestBackup;
        if (walStatus.IsEnabled)
        {
            var extended = latestBackup.AddMinutes(walStatus.LagMinutes ?? 0);
            latest = extended > now ? now : extended;
        }

        return new PitrAvailabilityResponseDto
        {
            IsAvailable = true,
            TenantIdFilter = tenantId,
            Message = BuildAvailabilityMessage(tenantId, walStatus.IsEnabled),
            EarliestRestorePointUtc = earliest,
            LatestRestorePointUtc = latest,
            SupportedTimePointsUtc = completed,
            WalArchivingEnabled = walStatus.IsEnabled,
            WalArchiveLagMinutes = walStatus.LagMinutes
        };
    }

    public async Task<RestorePointValidationResultDto> ValidateRestorePointAsync(
        Guid? tenantId,
        DateTime targetTimeUtc,
        CancellationToken cancellationToken = default)
    {
        var target = NormalizeUtc(targetTimeUtc);
        var now = _time.GetUtcNow().UtcDateTime;

        if (target > now.AddMinutes(1))
        {
            return Invalid(
                tenantId,
                $"Target time {target:O} is in the future.");
        }

        var availability = await GetPitrAvailabilityAsync(tenantId, cancellationToken);
        if (!availability.IsAvailable)
        {
            return Invalid(tenantId, availability.Message);
        }

        if (availability.EarliestRestorePointUtc.HasValue && target < availability.EarliestRestorePointUtc.Value)
        {
            return Invalid(
                tenantId,
                $"Target time {target:O} is before the earliest restore point {availability.EarliestRestorePointUtc:O}.");
        }

        if (availability.LatestRestorePointUtc.HasValue && target > availability.LatestRestorePointUtc.Value)
        {
            return Invalid(
                tenantId,
                $"Target time {target:O} is after the latest restore point {availability.LatestRestorePointUtc:O}.");
        }

        var backup = await BackupRunTenantSlugResolver.ApplyTenantHint(
                _db.BackupRuns.AsNoTracking()
                    .Where(r => r.Status == BackupRunStatus.Succeeded
                                && r.CompletedAt != null
                                && r.CompletedAt <= target),
                tenantId)
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (backup?.CompletedAt == null)
        {
            return Invalid(
                tenantId,
                $"No backup found before {target:O}.");
        }

        var walStatus = await GetWalArchiveStatusAsync(cancellationToken);
        var walCoverage = walStatus.IsEnabled
                          && await CheckWalCoverageAsync(backup.CompletedAt.Value, target, cancellationToken);

        var dataLossSeconds = Math.Max(0, (int)Math.Round((target - backup.CompletedAt.Value).TotalSeconds));

        return new RestorePointValidationResultDto
        {
            IsValid = true,
            TenantIdFilter = tenantId,
            Message = walCoverage
                ? "Target time is covered by base backup plus declared WAL archiving."
                : "Target time requires restoring to the nearest base backup only (WAL archiving not declared or unavailable).",
            BaseBackupId = backup.Id,
            BaseBackupTimeUtc = backup.CompletedAt.Value,
            TargetTimeUtc = target,
            WalCoverageStartUtc = backup.CompletedAt.Value,
            WalCoverageEndUtc = target,
            EstimatedDataLossSeconds = dataLossSeconds,
            RecoveryMethod = walCoverage ? RecoveryMethodPitr : RecoveryMethodFullBackupOnly
        };
    }

    private async Task<IReadOnlyList<BackupRun>> LoadSucceededBackupsAsync(
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        return await BackupRunTenantSlugResolver.ApplyTenantHint(
                _db.BackupRuns.AsNoTracking()
                    .Where(r => r.Status == BackupRunStatus.Succeeded && r.CompletedAt != null),
                tenantId)
            .OrderBy(r => r.CompletedAt)
            .ToListAsync(cancellationToken);
    }

    private async Task<PitrWalArchiveStatus> GetWalArchiveStatusAsync(CancellationToken cancellationToken)
    {
        var opts = _backupOptions.CurrentValue;
        var hasWalArtifacts = await _db.BackupArtifacts.AsNoTracking()
            .AnyAsync(a => a.ArtifactType == BackupArtifactType.WalArchiveWindow, cancellationToken);

        var enabled = opts.PitrWalArchivingDeclaredEnabled || hasWalArtifacts;
        var lag = opts.PitrWalArchiveDeclaredLagMinutes;
        if (enabled && lag is null or < 0)
            lag = 5;

        return new PitrWalArchiveStatus(enabled, lag);
    }

    private Task<bool> CheckWalCoverageAsync(
        DateTime walCoverageStart,
        DateTime target,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (target <= walCoverageStart)
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static RestorePointValidationResultDto Invalid(Guid? tenantId, string message) =>
        new()
        {
            IsValid = false,
            TenantIdFilter = tenantId,
            Message = message,
            RecoveryMethod = string.Empty
        };

    private static string BuildAvailabilityMessage(Guid? tenantId, bool walEnabled)
    {
        var scope = tenantId.HasValue
            ? "Tenant filter applies only to manual backups encoded in idempotency keys; scheduled backups remain instance-wide."
            : "Restore window is based on instance-wide succeeded backup runs.";
        var wal = walEnabled
            ? "Declared WAL archiving extends the latest restore point toward now."
            : "WAL archiving is not declared; restore points align with base backup completion times only.";
        return $"{scope} {wal}";
    }

    private sealed record PitrWalArchiveStatus(bool IsEnabled, int? LagMinutes);
}
