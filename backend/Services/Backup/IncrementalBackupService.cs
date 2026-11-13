using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Phase 2: tenant incremental (delta) backup facade — enqueue only; export runs on the worker.
/// </summary>
public sealed class IncrementalBackupService : IIncrementalBackupService
{
    public const string TenantNotFoundCode = BackupService.TenantNotFoundCode;
    public const string InvalidSinceCode = "INCREMENTAL_SINCE_INVALID";
    public const string NoChangesCode = "INCREMENTAL_NO_CHANGES";

    private readonly AppDbContext _db;
    private readonly IBackupManualTriggerService _manualTrigger;
    private readonly IBackupStagingDiskMonitor _diskMonitor;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly ILogger<IncrementalBackupService> _logger;
    private readonly IRestoreVerificationManualTriggerService? _drill;

    public IncrementalBackupService(
        AppDbContext db,
        IBackupManualTriggerService manualTrigger,
        IBackupStagingDiskMonitor diskMonitor,
        IOptionsMonitor<BackupOptions> options,
        ILogger<IncrementalBackupService> logger,
        IRestoreVerificationManualTriggerService? drill = null)
    {
        _db = db;
        _manualTrigger = manualTrigger;
        _diskMonitor = diskMonitor;
        _options = options;
        _logger = logger;
        _drill = drill;
    }

    public async Task<IncrementalChangeSummary> GetChangesSinceAsync(
        Guid tenantId,
        DateTime lastFullBackupUtc,
        CancellationToken ct = default)
    {
        var since = NormalizeSinceUtc(lastFullBackupUtc);
        var counts = await TenantIncrementalChangeCounter.CountAsync(_db, tenantId, since, ct)
            .ConfigureAwait(false);
        return new IncrementalChangeSummary
        {
            TenantId = tenantId,
            SinceUtc = since,
            TableChangeCounts = counts,
        };
    }

    public async Task<BackupTriggerResult> CreateIncrementalBackupAsync(
        Guid tenantId,
        Guid userId,
        DateTime lastFullBackupUtc,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return BackupTriggerResult.Fail(TenantNotFoundCode, "Tenant id is required.");

        var tenant = await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false);
        if (tenant == null)
            return BackupTriggerResult.Fail(TenantNotFoundCode, "Tenant not found.");

        DateTime since;
        try
        {
            since = NormalizeSinceUtc(lastFullBackupUtc);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BackupTriggerResult.Fail(InvalidSinceCode, ex.Message);
        }

        var budget = await EnsureStorageBudgetAsync(ct).ConfigureAwait(false);
        if (budget != null)
            return budget;

        IncrementalChangeSummary? changes = null;
        if (userId != Guid.Empty)
        {
            changes = await GetChangesSinceAsync(tenantId, since, ct).ConfigureAwait(false);
            if (changes.TotalChangedRows == 0)
            {
                return BackupTriggerResult.Fail(
                    NoChangesCode,
                    $"No tenant data changes since {since:O}; incremental package not enqueued.");
            }
        }

        // ✅ Only enqueue — worker exports changed rows since watermark (much smaller than full ZIP).
        var idempotencyKey =
            $"manual-tenant-incr-{tenantId:D}-{since:yyyyMMddHHmmss}-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var actor = userId == Guid.Empty ? "system" : userId.ToString();
        var role = userId == Guid.Empty ? "System" : Roles.Manager;
        var outcome = await _manualTrigger.RequestManualBackupAsync(
                actor,
                role,
                idempotencyKey,
                correlationId: $"backup-tenant-incr-{Guid.NewGuid():N}",
                strategy: BackupStrategyKind.Tenant,
                deploymentWide: false,
                cancellationToken: ct,
                incrementalSinceUtc: since)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "IncrementalBackupService enqueue: tenantId={TenantId}, slug={Slug}, userId={UserId}, runId={RunId}, sinceUtc={SinceUtc}, changedRows={ChangedRows}, kind={Kind}",
            tenantId,
            tenant.Slug,
            userId,
            outcome.Run.Id,
            since,
            changes?.TotalChangedRows,
            outcome.Kind);

        return BackupTriggerResult.Success(outcome.Run.Id, outcome.Kind);
    }

    private async Task<BackupTriggerResult?> EnsureStorageBudgetAsync(CancellationToken ct)
    {
        var usedBytes = await (
                from a in _db.BackupArtifacts.AsNoTracking()
                join r in _db.BackupRuns.AsNoTracking() on a.BackupRunId equals r.Id
                where a.ArtifactType == BackupArtifactType.LogicalDump
                      && a.ByteSize != null
                      && r.Status == BackupRunStatus.Succeeded
                select a.ByteSize!.Value)
            .SumAsync(ct)
            .ConfigureAwait(false);

        if (usedBytes >= BackupService.MaxStorageBytes)
        {
            return BackupTriggerResult.Fail(
                BackupService.StorageLimitCode,
                $"Backup storage budget exceeded ({usedBytes} bytes >= {BackupService.MaxStorageBytes} bytes). Reduce retention or free artifacts.");
        }

        var opts = _options.CurrentValue;
        var disk = _diskMonitor.TryGetUsage(opts.ArtifactStagingRoot, opts.StagingDiskUsageAlertPercent);
        if (disk is { Alert: true })
        {
            return BackupTriggerResult.Fail(
                BackupService.StagingDiskFullCode,
                $"Staging disk at {disk.UsedPercent}% (alert threshold {opts.StagingDiskUsageAlertPercent}%). Free space before enqueueing.");
        }

        return null;
    }

    public async Task<IncrementalRestorePlanDto> PlanRestoreFromIncrementalAsync(
        Guid tenantId,
        DateTime? targetUtc = null,
        CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false);
        if (tenant == null)
        {
            return new IncrementalRestorePlanDto
            {
                TenantId = tenantId,
                Message = "Tenant not found."
            };
        }

        var cutoff = targetUtc.HasValue
            ? NormalizeUtc(targetUtc.Value)
            : DateTime.UtcNow;

        var tenantRuns = await _db.BackupRuns.AsNoTracking()
            .Where(r => r.TenantId == tenantId
                        && r.Strategy == BackupStrategyKind.Tenant
                        && r.Status == BackupRunStatus.Succeeded
                        && r.CompletedAt != null
                        && r.CompletedAt <= cutoff)
            .OrderBy(r => r.CompletedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        BackupRun? full = null;
        foreach (var run in tenantRuns)
        {
            if (!BackupIncrementalPackageMetadata.TryReadIncrementalSinceUtc(run.ConfigSnapshotJson, out _))
                full = run;
        }

        var incrementals = new List<Guid>();
        if (full != null)
        {
            foreach (var run in tenantRuns.Where(r => r.CompletedAt >= full.CompletedAt && r.Id != full.Id))
            {
                if (BackupIncrementalPackageMetadata.TryReadIncrementalSinceUtc(run.ConfigSnapshotJson, out _))
                    incrementals.Add(run.Id);
            }
        }

        var systemDump = await _db.BackupRuns.AsNoTracking()
            .Where(r => r.Strategy == BackupStrategyKind.System
                        && r.Status == BackupRunStatus.Succeeded
                        && r.CompletedAt != null
                        && r.CompletedAt <= cutoff)
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return new IncrementalRestorePlanDto
        {
            TenantId = tenantId,
            FullBackupId = full?.Id,
            FullBackupCompletedAtUtc = full?.CompletedAt,
            IncrementalBackupIds = incrementals,
            NearestSystemDumpId = systemDump?.Id,
            Message = full == null
                ? "No succeeded full Tenant backup found before the target time."
                : "Restore plan is full Tenant ZIP plus later incrementals. Isolated rehearsal uses the nearest System dump; tenant packages are not pg_restore input."
        };
    }

    public async Task<IncrementalRestoreResultDto> RestoreFromIncrementalAsync(
        Guid tenantId,
        DateTime targetUtc,
        string actorUserId,
        CancellationToken ct = default)
    {
        var plan = await PlanRestoreFromIncrementalAsync(tenantId, targetUtc, ct).ConfigureAwait(false);
        if (plan.NearestSystemDumpId is not Guid dumpId)
        {
            return new IncrementalRestoreResultDto
            {
                Plan = plan,
                Message = "No succeeded System dump is available for isolated restore. Tenant incremental ZIP cannot be applied with pg_restore."
            };
        }

        if (_drill == null)
        {
            return new IncrementalRestoreResultDto
            {
                Plan = plan,
                Message = plan.Message
            };
        }

        var drill = await _drill.EnqueueManualAsync(
                actorUserId,
                $"pitr-incr-{Guid.NewGuid():N}",
                $"pitr-incr-{tenantId:N}-{dumpId:N}",
                dumpId,
                ct)
            .ConfigureAwait(false);

        return new IncrementalRestoreResultDto
        {
            Plan = plan,
            IsolatedDryRunEnqueued = true,
            DrillRunId = drill.Run.Id,
            Message = "Isolated restore drill enqueued for the nearest System dump. Tenant incrementals remain evidence packages, not pg_restore input. Production is not modified."
        };
    }

    public async Task<int> EnqueueDueDailyIncrementalsAsync(CancellationToken ct = default)
    {
        if (!_options.CurrentValue.IncrementalBackupEnabled)
            return 0;

        var tenants = await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var enqueued = 0;
        foreach (var tenantId in tenants)
        {
            var lastFull = await _db.BackupRuns.AsNoTracking()
                .Where(r => r.TenantId == tenantId
                            && r.Strategy == BackupStrategyKind.Tenant
                            && r.Status == BackupRunStatus.Succeeded
                            && r.CompletedAt != null)
                .OrderByDescending(r => r.CompletedAt)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var full = lastFull.FirstOrDefault(r =>
                !BackupIncrementalPackageMetadata.TryReadIncrementalSinceUtc(r.ConfigSnapshotJson, out _));
            if (full?.CompletedAt == null)
                continue;

            var lastIncremental = lastFull.FirstOrDefault(r =>
                BackupIncrementalPackageMetadata.TryReadIncrementalSinceUtc(r.ConfigSnapshotJson, out _)
                && r.CompletedAt >= full.CompletedAt);
            if (lastIncremental?.CompletedAt != null
                && lastIncremental.CompletedAt.Value > DateTime.UtcNow.AddHours(-20))
            {
                continue;
            }

            var result = await CreateIncrementalBackupAsync(tenantId, Guid.Empty, full.CompletedAt.Value, ct)
                .ConfigureAwait(false);
            if (result.Succeeded)
                enqueued++;
        }

        return enqueued;
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    internal static DateTime NormalizeSinceUtc(DateTime lastFullBackupUtc)
    {
        var since = lastFullBackupUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(lastFullBackupUtc, DateTimeKind.Utc)
            : lastFullBackupUtc.ToUniversalTime();

        if (since > DateTime.UtcNow.AddMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(lastFullBackupUtc), "lastFullBackup must not be in the future.");

        if (since < DateTime.UtcNow.AddYears(-20))
            throw new ArgumentOutOfRangeException(nameof(lastFullBackupUtc), "lastFullBackup is unreasonably old.");

        return since;
    }
}
