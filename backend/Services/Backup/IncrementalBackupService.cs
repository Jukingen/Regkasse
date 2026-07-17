using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
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

    public IncrementalBackupService(
        AppDbContext db,
        IBackupManualTriggerService manualTrigger,
        IBackupStagingDiskMonitor diskMonitor,
        IOptionsMonitor<BackupOptions> options,
        ILogger<IncrementalBackupService> logger)
    {
        _db = db;
        _manualTrigger = manualTrigger;
        _diskMonitor = diskMonitor;
        _options = options;
        _logger = logger;
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

    public async Task<BackupResult> CreateIncrementalBackupAsync(
        Guid tenantId,
        Guid userId,
        DateTime lastFullBackupUtc,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return BackupResult.Fail(TenantNotFoundCode, "Tenant id is required.");

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false);
        if (tenant == null)
            return BackupResult.Fail(TenantNotFoundCode, "Tenant not found.");

        DateTime since;
        try
        {
            since = NormalizeSinceUtc(lastFullBackupUtc);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BackupResult.Fail(InvalidSinceCode, ex.Message);
        }

        var budget = await EnsureStorageBudgetAsync(ct).ConfigureAwait(false);
        if (budget != null)
            return budget;

        var changes = await GetChangesSinceAsync(tenantId, since, ct).ConfigureAwait(false);
        if (changes.TotalChangedRows == 0)
        {
            return BackupResult.Fail(
                NoChangesCode,
                $"No tenant data changes since {since:O}; incremental package not enqueued.");
        }

        // ✅ Only enqueue — worker exports changed rows since watermark (much smaller than full ZIP).
        var idempotencyKey =
            $"manual-tenant-incr-{tenantId:D}-{since:yyyyMMddHHmmss}-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var outcome = await _manualTrigger.RequestManualBackupAsync(
                userId.ToString(),
                Roles.Manager,
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
            changes.TotalChangedRows,
            outcome.Kind);

        return BackupResult.Success(outcome.Run.Id, outcome.Kind);
    }

    private async Task<BackupResult?> EnsureStorageBudgetAsync(CancellationToken ct)
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
            return BackupResult.Fail(
                BackupService.StorageLimitCode,
                $"Backup storage budget exceeded ({usedBytes} bytes >= {BackupService.MaxStorageBytes} bytes). Reduce retention or free artifacts.");
        }

        var opts = _options.CurrentValue;
        var disk = _diskMonitor.TryGetUsage(opts.ArtifactStagingRoot, opts.StagingDiskUsageAlertPercent);
        if (disk is { Alert: true })
        {
            return BackupResult.Fail(
                BackupService.StagingDiskFullCode,
                $"Staging disk at {disk.UsedPercent}% (alert threshold {opts.StagingDiskUsageAlertPercent}%). Free space before enqueueing.");
        }

        return null;
    }

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
