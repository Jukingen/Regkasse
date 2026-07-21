using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Facade over manual backup enqueue + RKSV validation-only restore request.
/// Tenant strategy enqueues a worker job that exports tenant-only JSON ZIP (no Identity).
/// System strategy enqueues instance-wide <c>pg_dump</c>. Never runs dump/export on the HTTP thread.
/// </summary>
public sealed class BackupService : IBackupService
{
    public const int DefaultRetentionDays = BackupStrategyPolicy.TenantRetentionDays;
    public const int SystemDefaultRetentionDays = BackupStrategyPolicy.SystemRetentionDays;
    public const long MaxStorageBytes = 10L * 1024 * 1024 * 1024;

    public const string TenantNotFoundCode = "TENANT_NOT_FOUND";
    public const string StorageLimitCode = "BACKUP_STORAGE_LIMIT";
    public const string StagingDiskFullCode = "STAGING_DISK_ALERT";
    public const string BackupNotFoundCode = ComplianceCheckService.BackupNotFoundCode;
    public const string IntegrityFailedCode = ComplianceCheckService.IntegrityFailedCode;
    public const string IntegrityHashMissingCode = ComplianceCheckService.IntegrityHashMissingCode;

    private readonly AppDbContext _db;
    private readonly IBackupManualTriggerService _manualTrigger;
    private readonly IComplianceCheckService _complianceCheck;
    private readonly IManualRestoreTriggerService _manualRestore;
    private readonly IBackupStagingDiskMonitor _diskMonitor;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        AppDbContext db,
        IBackupManualTriggerService manualTrigger,
        IComplianceCheckService complianceCheck,
        IManualRestoreTriggerService manualRestore,
        IBackupStagingDiskMonitor diskMonitor,
        IOptionsMonitor<BackupOptions> options,
        ILogger<BackupService> logger)
    {
        _db = db;
        _manualTrigger = manualTrigger;
        _complianceCheck = complianceCheck;
        _manualRestore = manualRestore;
        _diskMonitor = diskMonitor;
        _options = options;
        _logger = logger;
    }

    public Task<BackupResult> CreateBackupAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default) =>
        CreateTenantBackupAsync(tenantId, userId, ct);

    /// <summary>
    /// Mandanten-Admin path: validates tenant, then enqueues a Tenant-strategy run.
    /// Worker writes tenant-only JSON ZIP (payments/receipts/products/…; no Identity / platform users).
    /// </summary>
    public async Task<BackupResult> CreateTenantBackupAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default)
    {
        // ✅ Only enqueue for a known tenant — row extract runs off-request on the worker.
        if (tenantId == Guid.Empty)
            return BackupResult.Fail(TenantNotFoundCode, "Tenant id is required.");

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant == null)
            return BackupResult.Fail(TenantNotFoundCode, "Tenant not found.");

        var budget = await EnsureStorageBudgetAsync(ct);
        if (budget != null)
            return budget;

        // ✅ NO system data on this path: strategy=Tenant → TenantScopedLogicalBackupExecutionAdapter
        var idempotencyKey = $"manual-tenant-{tenantId:D}-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var outcome = await _manualTrigger.RequestManualBackupAsync(
            userId.ToString(),
            Roles.Manager,
            idempotencyKey,
            correlationId: $"backup-tenant-{Guid.NewGuid():N}",
            strategy: BackupStrategyKind.Tenant,
            deploymentWide: false,
            cancellationToken: ct);

        _logger.LogInformation(
            "BackupService tenant enqueue: tenantId={TenantId}, slug={Slug}, userId={UserId}, runId={RunId}, kind={Kind}, format=tenant-logical-zip",
            tenantId,
            tenant.Slug,
            userId,
            outcome.Run.Id,
            outcome.Kind);

        return BackupResult.Success(outcome.Run.Id, outcome.Kind);
    }

    /// <summary>
    /// Super Admin path: validates storage budget, then enqueues a System-strategy run.
    /// Worker produces <c>pg_dump</c> (restore) + structured <c>*.system.zip</c>
    /// (all active tenants, Identity, platform settings, deployment licenses, audit).
    /// </summary>
    public async Task<BackupResult> CreateSystemBackupAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        // ✅ Enqueue only — worker builds SystemBackupData package + instance dump (AGENTS.md).
        var budget = await EnsureStorageBudgetAsync(ct);
        if (budget != null)
            return budget;

        var activeTenantCount = await _db.Tenants.AsNoTracking()
            .CountAsync(t => t.IsActive, ct);

        var idempotencyKey = $"manual-system-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var outcome = await _manualTrigger.RequestManualBackupAsync(
            userId.ToString(),
            Roles.SuperAdmin,
            idempotencyKey,
            correlationId: $"backup-system-{Guid.NewGuid():N}",
            strategy: BackupStrategyKind.System,
            deploymentWide: true,
            cancellationToken: ct);

        _logger.LogInformation(
            "BackupService system enqueue: userId={UserId}, runId={RunId}, kind={Kind}, activeTenants={ActiveTenants}, retentionDaysDefault={RetentionDays}, format=pg_dump+system-zip",
            userId,
            outcome.Run.Id,
            outcome.Kind,
            activeTenantCount,
            BackupStrategyPolicy.SystemRetentionDays);

        return BackupResult.Success(outcome.Run.Id, outcome.Kind);
    }

    public async Task<BackupListResult> ListBackupsAsync(
        Guid? tenantId,
        bool isSuperAdmin,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.BackupRuns.AsNoTracking();

        if (isSuperAdmin)
        {
            // ✅ SuperAdmin sees all backups (Tenant + System)
        }
        else
        {
            // ✅ TenantAdmin sees only own tenant strategy rows — never System (Identity / all-tenants).
            if (tenantId is not Guid tid || tid == Guid.Empty)
            {
                return new BackupListResult
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = 0,
                    Items = Array.Empty<BackupListItem>()
                };
            }

            query = BackupRunAccessEvaluator.ApplyTenantScopeFilter(query, tid);
        }

        var total = await query.CountAsync(ct);
        var runs = await query
            .OrderByDescending(r => r.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.Strategy,
                r.TenantId,
                r.Status,
                r.RequestedAt,
                r.CompletedAt,
                LogicalDumpBytes = r.Artifacts
                    .Where(a => a.ArtifactType == BackupArtifactType.LogicalDump)
                    .Select(a => (long?)a.ByteSize)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return new BackupListResult
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = runs.Select(r => new BackupListItem
            {
                BackupRunId = r.Id,
                Strategy = r.Strategy,
                TenantId = r.TenantId,
                Status = r.Status,
                RequestedAt = r.RequestedAt,
                CompletedAt = r.CompletedAt,
                LogicalDumpBytes = r.LogicalDumpBytes
            }).ToList()
        };
    }

    public async Task<RestoreResult> RestoreBackupAsync(
        Guid backupId,
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default)
    {
        var compliance = await _complianceCheck.CheckRestoreComplianceAsync(backupId, tenantId, ct);
        if (!compliance.Succeeded)
        {
            return RestoreResult.Fail(
                compliance.Code ?? "COMPLIANCE_FAILED",
                compliance.Error ?? "Restore compliance check failed.");
        }

        var targetDb = $"restore_validation_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var status = await _manualRestore.CreateRequestAsync(
            new RestoreRequest
            {
                BackupRunId = backupId,
                TargetDatabaseName = targetDb,
                ValidationOnly = true,
                Reason = "BackupService RKSV validation restore"
            },
            userId.ToString(),
            actorEmail: null,
            correlationId: $"restore-service-{Guid.NewGuid():N}",
            ct);

        _logger.LogInformation(
            "BackupService restore request: backupId={BackupId}, tenantId={TenantId}, userId={UserId}, requestId={RequestId}",
            backupId,
            tenantId,
            userId,
            status.RequestId);

        return RestoreResult.SuccessQueued(backupId, status.RequestId, targetDb);
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
            .SumAsync(ct);
        if (usedBytes >= MaxStorageBytes)
        {
            return BackupResult.Fail(
                StorageLimitCode,
                $"Backup storage budget exceeded ({usedBytes} bytes >= {MaxStorageBytes} bytes). Reduce retention or free artifacts.");
        }

        var opts = _options.CurrentValue;
        var disk = _diskMonitor.TryGetUsage(opts.ArtifactStagingRoot, opts.StagingDiskUsageAlertPercent);
        if (disk is { Alert: true })
        {
            return BackupResult.Fail(
                StagingDiskFullCode,
                $"Staging disk at {disk.UsedPercent}% (alert threshold {opts.StagingDiskUsageAlertPercent}%). Free space before enqueueing.");
        }

        return null;
    }
}
