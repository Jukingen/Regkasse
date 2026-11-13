using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Point-in-time recovery planning: succeeded runs, WAL archive inventory, pre-restore checks,
/// and isolated dry-run enqueue. Never restores production.
/// </summary>
public sealed class PitrService : IPitrService
{
    public const string RecoveryMethodPitr = "PITR";
    public const string RecoveryMethodFullBackupOnly = "FullBackupOnly";
    public const string RecoveryMethodFullPlusIncremental = "FullPlusIncremental";

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<BackupOptions> _backupOptions;
    private readonly TimeProvider _time;
    private readonly ILogger<PitrService> _logger;
    private readonly IWalArchiveService? _walArchive;
    private readonly IBackupChecksumVerificationService? _checksum;
    private readonly IBackupContentValidationService? _content;
    private readonly IRestoreVerificationManualTriggerService? _drill;
    private readonly IBackupChainService? _chain;

    public PitrService(
        AppDbContext db,
        IOptionsMonitor<BackupOptions> backupOptions,
        TimeProvider time,
        ILogger<PitrService> logger,
        IWalArchiveService? walArchive = null,
        IBackupChecksumVerificationService? checksum = null,
        IBackupContentValidationService? content = null,
        IRestoreVerificationManualTriggerService? drill = null,
        IBackupChainService? chain = null)
    {
        _db = db;
        _backupOptions = backupOptions;
        _time = time;
        _logger = logger;
        _walArchive = walArchive;
        _checksum = checksum;
        _content = content;
        _drill = drill;
        _chain = chain;
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

        var walStatus = GetWalArchiveStatus();
        var completed = backups
            .Where(b => b.CompletedAt.HasValue)
            .Select(b => b.CompletedAt!.Value)
            .ToList();

        var earliest = completed.Min();
        var latestBackup = completed.Max();
        var now = _time.GetUtcNow().UtcDateTime;
        var latest = latestBackup;
        if (walStatus.Enabled)
        {
            var extended = latestBackup.AddMinutes(walStatus.LagMinutes ?? 0);
            if (walStatus.NewestFileUtc.HasValue && walStatus.NewestFileUtc.Value > extended)
                extended = walStatus.NewestFileUtc.Value;
            latest = extended > now ? now : extended;
        }

        return new PitrAvailabilityResponseDto
        {
            IsAvailable = true,
            TenantIdFilter = tenantId,
            Message = BuildAvailabilityMessage(tenantId, walStatus.Enabled),
            EarliestRestorePointUtc = earliest,
            LatestRestorePointUtc = latest,
            SupportedTimePointsUtc = completed,
            WalArchivingEnabled = walStatus.Enabled,
            WalArchiveLagMinutes = walStatus.LagMinutes,
            WalFileCount = walStatus.FileCount,
            WalRetentionDays = walStatus.RetentionDays,
            WalCoverageStartUtc = walStatus.OldestFileUtc,
            WalCoverageEndUtc = walStatus.NewestFileUtc
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

        var walCoverage = CheckWalCoverage(backup.CompletedAt.Value, target);
        var incrementals = await LoadIncrementalsAfterAsync(backup, target, cancellationToken)
            .ConfigureAwait(false);
        var dataLossSeconds = Math.Max(0, (int)Math.Round((target - backup.CompletedAt.Value).TotalSeconds));
        var method = walCoverage
            ? RecoveryMethodPitr
            : incrementals.Count > 0
                ? RecoveryMethodFullPlusIncremental
                : RecoveryMethodFullBackupOnly;

        return new RestorePointValidationResultDto
        {
            IsValid = true,
            TenantIdFilter = tenantId,
            Message = method switch
            {
                RecoveryMethodPitr =>
                    "Target time is covered by base backup plus WAL archiving (host archive_command).",
                RecoveryMethodFullPlusIncremental =>
                    "Target time uses the nearest full backup plus later incremental Tenant packages. Isolated restore still applies the System dump only.",
                _ =>
                    "Target time requires restoring to the nearest base backup only (WAL archiving not declared or unavailable)."
            },
            BaseBackupId = backup.Id,
            BaseBackupTimeUtc = backup.CompletedAt.Value,
            TargetTimeUtc = target,
            WalCoverageStartUtc = backup.CompletedAt.Value,
            WalCoverageEndUtc = target,
            EstimatedDataLossSeconds = dataLossSeconds,
            RecoveryMethod = method,
            FullBackupId = backup.Id,
            IncrementalBackupIds = incrementals
        };
    }

    public async Task<PitrPreRestoreValidationDto> ValidatePreRestoreAsync(
        Guid? tenantId,
        DateTime targetTimeUtc,
        CancellationToken cancellationToken = default)
    {
        var point = await ValidateRestorePointAsync(tenantId, targetTimeUtc, cancellationToken)
            .ConfigureAwait(false);
        if (!point.IsValid || point.BaseBackupId is not Guid baseId)
        {
            return new PitrPreRestoreValidationDto
            {
                Passed = false,
                TargetTimeUtc = point.TargetTimeUtc,
                RestorePoint = point,
                Message = point.Message
            };
        }

        var hash = await RunHashCheckAsync(baseId, cancellationToken).ConfigureAwait(false);
        var content = await RunContentChecksAsync(baseId, cancellationToken).ConfigureAwait(false);
        BackupChainResponseDto? chain = null;
        if (_chain != null)
        {
            chain = await _chain.GetChainAsync(
                    tenantId,
                    new BackupRunAccessScope(true, tenantId, null),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var passed = point.IsValid && hash.Passed && content.Schema.Passed && content.Tse.Passed;
        return new PitrPreRestoreValidationDto
        {
            Passed = passed,
            TargetTimeUtc = point.TargetTimeUtc,
            BaseBackupId = baseId,
            RecoveryMethod = point.RecoveryMethod,
            EstimatedDataLossSeconds = point.EstimatedDataLossSeconds,
            Hash = hash,
            Schema = content.Schema,
            TseChain = content.Tse,
            Chain = chain,
            RestorePoint = point,
            Message = passed
                ? "Pre-restore checks passed (hash, schema/content, TSE). Isolated dry-run is still required before any operator recovery."
                : "One or more pre-restore checks failed. See hash, schema, and TSE results."
        };
    }

    public async Task<PitrDryRunResponseDto> RequestDryRunAsync(
        Guid? tenantId,
        DateTime targetTimeUtc,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidatePreRestoreAsync(tenantId, targetTimeUtc, cancellationToken)
            .ConfigureAwait(false);
        if (validation.BaseBackupId is not Guid baseId)
        {
            return new PitrDryRunResponseDto
            {
                Accepted = false,
                Validation = validation,
                TargetTimeUtc = validation.TargetTimeUtc,
                Message = validation.Message
            };
        }

        if (_drill == null)
        {
            return new PitrDryRunResponseDto
            {
                Accepted = false,
                BaseBackupId = baseId,
                TargetTimeUtc = validation.TargetTimeUtc,
                Validation = validation,
                Message = "Restore drill service is not registered."
            };
        }

        var systemDumpId = await ResolveSystemDumpIdAsync(baseId, validation.TargetTimeUtc, cancellationToken)
            .ConfigureAwait(false);
        if (systemDumpId == null)
        {
            return new PitrDryRunResponseDto
            {
                Accepted = false,
                BaseBackupId = baseId,
                TargetTimeUtc = validation.TargetTimeUtc,
                Validation = validation,
                Message = "No succeeded System dump is available for isolated pg_restore. Tenant ZIP cannot be restored with pg_restore."
            };
        }

        var drill = await _drill.EnqueueManualAsync(
                actorUserId,
                $"pitr-dryrun-{Guid.NewGuid():N}",
                $"pitr-dryrun-{systemDumpId:N}-{DateTime.UtcNow:yyyyMMddHHmm}",
                systemDumpId,
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "PITR dry-run enqueued: drillRunId={DrillRunId}, dumpId={DumpId}, target={Target:O}",
            drill.Run.Id,
            systemDumpId,
            validation.TargetTimeUtc);

        return new PitrDryRunResponseDto
        {
            Accepted = true,
            DrillRunId = drill.Run.Id,
            BaseBackupId = systemDumpId,
            TargetTimeUtc = validation.TargetTimeUtc,
            Validation = validation,
            Message = "Isolated restore drill enqueued (restore_validation_* / rv_v_*). Production is not modified. WAL replay is a host DBA procedure after the dump restore."
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

    private WalArchiveStatusDto GetWalArchiveStatus()
    {
        if (_walArchive != null)
            return _walArchive.GetStatus();

        var opts = _backupOptions.CurrentValue;
        var enabled = opts.PitrWalArchivingDeclaredEnabled;
        var lag = opts.PitrWalArchiveDeclaredLagMinutes;
        if (enabled && lag is null or < 0)
            lag = 5;

        return new WalArchiveStatusDto
        {
            Enabled = enabled,
            LagMinutes = lag,
            RetentionDays = Math.Max(1, opts.WalArchiveRetentionDays),
            SwitchIntervalMinutes = Math.Max(1, opts.WalArchiveSwitchIntervalMinutes),
            Message = enabled
                ? "Declared WAL archiving (no archive directory inventory)."
                : "WAL archiving is not declared."
        };
    }

    private bool CheckWalCoverage(DateTime walCoverageStart, DateTime target)
    {
        if (target <= walCoverageStart)
            return false;

        if (_walArchive != null)
            return _walArchive.CoversWindow(walCoverageStart, target);

        return _backupOptions.CurrentValue.PitrWalArchivingDeclaredEnabled;
    }

    private async Task<IReadOnlyList<Guid>> LoadIncrementalsAfterAsync(
        BackupRun baseBackup,
        DateTime target,
        CancellationToken cancellationToken)
    {
        if (baseBackup.Strategy != BackupStrategyKind.Tenant || !baseBackup.TenantId.HasValue)
            return Array.Empty<Guid>();

        var later = await _db.BackupRuns.AsNoTracking()
            .Where(r => r.TenantId == baseBackup.TenantId
                        && r.Strategy == BackupStrategyKind.Tenant
                        && r.Status == BackupRunStatus.Succeeded
                        && r.CompletedAt != null
                        && r.CompletedAt > baseBackup.CompletedAt
                        && r.CompletedAt <= target)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return later
            .Where(r => BackupIncrementalPackageMetadata.TryReadIncrementalSinceUtc(r.ConfigSnapshotJson, out _))
            .Select(r => r.Id)
            .ToList();
    }

    private async Task<PitrCheckResultDto> RunHashCheckAsync(Guid runId, CancellationToken cancellationToken)
    {
        if (_checksum == null)
        {
            return new PitrCheckResultDto
            {
                Name = "hash",
                Passed = false,
                Status = "skipped",
                Detail = "Checksum service is not registered."
            };
        }

        try
        {
            var result = await _checksum.VerifyChecksumAsync(runId, cancellationToken).ConfigureAwait(false);
            return new PitrCheckResultDto
            {
                Name = "hash",
                Passed = result.IsValid,
                Status = result.IsValid ? "passed" : "failed",
                Detail = result.FailureReason ?? $"Verified {result.Artifacts.Count} artifact(s)."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PITR hash check failed for run {RunId}", runId);
            return new PitrCheckResultDto
            {
                Name = "hash",
                Passed = false,
                Status = "failed",
                Detail = ex.Message
            };
        }
    }

    private async Task<(PitrCheckResultDto Schema, PitrCheckResultDto Tse)> RunContentChecksAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var skippedSchema = new PitrCheckResultDto
        {
            Name = "schema",
            Passed = false,
            Status = "skipped",
            Detail = "Content validation service is not registered."
        };
        var skippedTse = new PitrCheckResultDto
        {
            Name = "tse_chain",
            Passed = false,
            Status = "skipped",
            Detail = "Content validation service is not registered."
        };
        if (_content == null)
            return (skippedSchema, skippedTse);

        try
        {
            var report = await _content.GetOrRunValidationAsync(runId, cancellationToken).ConfigureAwait(false);
            var schemaPassed = report.OverallStatus is BackupContentValidationStatuses.Passed
                or BackupContentValidationStatuses.Partial;
            var tsePassed = report.Fiscal?.Status is "passed" or "warning" or "skipped"
                || (report.FiscalChecks.Count > 0 && report.FiscalChecks.All(c => c.Passed));
            if (report.FiscalChecks.Count > 0)
                tsePassed = report.FiscalChecks.All(c => c.Passed);

            return (
                new PitrCheckResultDto
                {
                    Name = "schema",
                    Passed = schemaPassed,
                    Status = report.OverallStatus.ToLowerInvariant(),
                    Detail = report.Summary ?? "Manifest table keys compared to live catalog."
                },
                new PitrCheckResultDto
                {
                    Name = "tse_chain",
                    Passed = tsePassed,
                    Status = tsePassed ? "passed" : "failed",
                    Detail = report.Fiscal?.Detail
                             ?? string.Join("; ", report.FiscalChecks.Select(c => $"{c.CheckName}={(c.Passed ? "ok" : "fail")}"))
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PITR content checks failed for run {RunId}", runId);
            return (
                new PitrCheckResultDto { Name = "schema", Passed = false, Status = "failed", Detail = ex.Message },
                new PitrCheckResultDto { Name = "tse_chain", Passed = false, Status = "failed", Detail = ex.Message });
        }
    }

    private async Task<Guid?> ResolveSystemDumpIdAsync(
        Guid baseBackupId,
        DateTime? targetUtc,
        CancellationToken cancellationToken)
    {
        var baseRun = await _db.BackupRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == baseBackupId, cancellationToken)
            .ConfigureAwait(false);
        if (baseRun?.Strategy == BackupStrategyKind.System)
            return baseRun.Id;

        var cutoff = targetUtc ?? baseRun?.CompletedAt ?? DateTime.UtcNow;
        var system = await _db.BackupRuns.AsNoTracking()
            .Where(r => r.Strategy == BackupStrategyKind.System
                        && r.Status == BackupRunStatus.Succeeded
                        && r.CompletedAt != null
                        && r.CompletedAt <= cutoff)
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return system?.Id;
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
            ? "WAL archiving extends the latest restore point toward now when archive files or the declared flag are present."
            : "WAL archiving is not declared; restore points align with base backup completion times only.";
        return $"{scope} {wal}";
    }
}
