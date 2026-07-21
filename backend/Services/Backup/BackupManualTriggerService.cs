using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.OperationalRuns;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupManualTriggerService : IBackupManualTriggerService
{
    /// <summary>
    /// <c>pg_advisory_xact_lock</c> çifti — backup orchestrator worker anahtarlarından farklı olmalı.
    /// </summary>
    private const int ManualEnqueueAdvisoryLockKey1 = unchecked((int)0x426B4D6E); // "BkMn"

    private const int ManualEnqueueAdvisoryLockKey2 = 3;

    private readonly AppDbContext _db;
    private readonly IAuditLogService _audit;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IBackupAlertPublisher _alerts;
    private readonly ILogger<BackupManualTriggerService> _logger;

    public BackupManualTriggerService(
        AppDbContext db,
        IAuditLogService audit,
        ICurrentTenantAccessor tenantAccessor,
        IOptionsMonitor<BackupOptions> options,
        IBackupAlertPublisher alerts,
        ILogger<BackupManualTriggerService> logger)
    {
        _db = db;
        _audit = audit;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
        _options = options;
        _alerts = alerts;
    }

    public async Task<BackupManualTriggerOutcome> RequestManualBackupAsync(
        string? requestedByUserId,
        string requestedByRole,
        string? idempotencyKey,
        string? correlationId,
        BackupStrategyKind? strategy = null,
        bool deploymentWide = false,
        CancellationToken cancellationToken = default,
        DateTime? incrementalSinceUtc = null)
    {
        var normalizedIdempotency = deploymentWide
            ? (string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim())
            : NormalizeIdempotencyKeyForTenantScope(idempotencyKey);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await TryAcquirePostgresManualEnqueueSerializationAsync(_db, cancellationToken);

        if (normalizedIdempotency != null)
        {
            var existing = await _db.BackupRuns.AsNoTracking()
                .FirstOrDefaultAsync(r => r.IdempotencyKey == normalizedIdempotency, cancellationToken);
            if (existing != null)
            {
                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation(
                    "Backup manual trigger idempotent hit: key={Key}, runId={RunId}",
                    normalizedIdempotency,
                    existing.Id);
                return new BackupManualTriggerOutcome
                {
                    Run = existing,
                    Kind = BackupManualTriggerResultKind.IdempotentReplay
                };
            }
        }

        var manualScopeTenantId = deploymentWide
            ? null
            : BackupRunAccessEvaluator.ResolveManualTriggerScopeTenantId(
                _tenantAccessor.TenantId,
                normalizedIdempotency);

        var activeManual = await BackupRunAccessEvaluator.ApplyActiveManualConflictScope(
                _db.BackupRuns.AsNoTracking(),
                manualScopeTenantId)
            .Where(r => r.TriggerSource == BackupTriggerSource.Manual)
            .Where(r =>
                r.Status == BackupRunStatus.Queued
                || r.Status == BackupRunStatus.Running
                || r.Status == BackupRunStatus.AwaitingVerification)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeManual != null)
        {
            _logger.LogInformation(
                "Backup manual trigger suppressed — active run exists: runId={RunId}",
                activeManual.Id);
            _alerts.Publish(new BackupAlertEvent(
                BackupAlertKind.DuplicateExecutionPrevented,
                activeManual.Id,
                correlationId,
                "Manual backup already queued or running.",
                new Dictionary<string, string>
                {
                    ["activeRunId"] = activeManual.Id.ToString(),
                    ["activeRunStatus"] = activeManual.Status.ToString()
                }));
            await transaction.CommitAsync(cancellationToken);
            return new BackupManualTriggerOutcome
            {
                Run = activeManual,
                Kind = BackupManualTriggerResultKind.DuplicateActiveManualPrevented
            };
        }

        var opts = _options.CurrentValue;
        var pref = await _db.BackupRuntimeExecutionPreferences.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == BackupRuntimeExecutionPreference.SingletonId, cancellationToken);
        var adminMode = pref?.Mode ?? AdminBackupRuntimeExecutionMode.InheritFromConfiguration;
        var effectiveKind = BackupEffectiveExecutionAdapterResolver.ResolveEffectiveAdapterKind(opts, adminMode);
        var adapterKind = effectiveKind.ToString();

        Guid? runTenantId = null;
        if (!deploymentWide)
        {
            runTenantId = _tenantAccessor.TenantId is Guid ambientTenantId && ambientTenantId != Guid.Empty
                ? ambientTenantId
                : BackupRunTenantSlugResolver.TryParseTenantIdFromIdempotencyKey(
                    normalizedIdempotency,
                    out var parsedTenantId)
                    ? parsedTenantId
                    : null;
        }

        var resolvedStrategy = deploymentWide
            ? BackupStrategyKind.System
            : BackupStrategyPolicy.Resolve(runTenantId, strategy);

        if (resolvedStrategy == BackupStrategyKind.Tenant
            && (runTenantId is null || runTenantId == Guid.Empty))
        {
            throw new InvalidOperationException(
                "Tenant strategy requires a resolved tenant id (JWT tenant context or manual-tenant idempotency key).");
        }

        if (resolvedStrategy == BackupStrategyKind.System)
            runTenantId = deploymentWide ? null : runTenantId;

        if (incrementalSinceUtc.HasValue && resolvedStrategy != BackupStrategyKind.Tenant)
        {
            throw new InvalidOperationException(
                "Incremental package watermark is only valid for Tenant strategy backups.");
        }

        var configJson = OperationalRunConfigSnapshotBuilder.SerializeBackup(
            opts,
            "backup_manual_enqueue",
            DateTime.UtcNow,
            effectiveKind,
            adminMode,
            resolvedStrategy);
        if (incrementalSinceUtc.HasValue)
        {
            configJson = BackupIncrementalPackageMetadata.MergeIntoConfigSnapshot(
                configJson,
                incrementalSinceUtc.Value);
        }

        var run = new BackupRun
        {
            Status = BackupRunStatus.Queued,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = adapterKind,
            IdempotencyKey = normalizedIdempotency,
            TenantId = resolvedStrategy == BackupStrategyKind.System && deploymentWide
                ? null
                : runTenantId,
            Strategy = resolvedStrategy,
            RequestedByUserId = requestedByUserId,
            RequestedAt = DateTime.UtcNow,
            QueuedAt = DateTime.UtcNow,
            CorrelationId = correlationId,
            ConfigSnapshotJson = configJson
        };

        _db.BackupRuns.Add(run);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (normalizedIdempotency != null
                                           && IsBackupRunIdempotencyUniqueViolation(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            var replay = await _db.BackupRuns.AsNoTracking()
                .FirstOrDefaultAsync(r => r.IdempotencyKey == normalizedIdempotency, cancellationToken);
            if (replay == null)
                throw;

            _logger.LogInformation(
                "Backup manual trigger idempotent after unique conflict: key={Key}, runId={RunId}",
                normalizedIdempotency,
                replay.Id);
            return new BackupManualTriggerOutcome
            {
                Run = replay,
                Kind = BackupManualTriggerResultKind.IdempotentReplay
            };
        }

        await transaction.CommitAsync(cancellationToken);

        var actorId = requestedByUserId ?? "system";
        var auditTenantId = run.TenantId ?? _tenantAccessor.TenantId;
        await _audit.LogSystemOperationAsync(
            action: "BACKUP_MANUAL_ENQUEUED",
            entityType: "BackupRun",
            userId: actorId,
            userRole: requestedByRole,
            description:
            $"Manual backup run {run.Id} enqueued (adapter={adapterKind}, strategy={run.Strategy}).",
            notes: null,
            status: AuditLogStatus.Success,
            errorDetails: null,
            requestData: new { run.Id, run.IdempotencyKey, correlationId, strategy = run.Strategy.ToString() },
            responseData: new { run.Status },
            correlationIdOverride: correlationId,
            entityId: run.Id,
            tenantId: auditTenantId);

        return new BackupManualTriggerOutcome { Run = run, Kind = BackupManualTriggerResultKind.NewRunQueued };
    }

    /// <summary>
    /// When ambient tenant context exists, ensure manual runs encode tenant in the idempotency key
    /// so tenant-scoped download ACL can resolve deployment-wide <see cref="BackupRun"/> rows.
    /// </summary>
    private string? NormalizeIdempotencyKeyForTenantScope(string? idempotencyKey)
    {
        var normalized = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        var ambientTenantId = _tenantAccessor.TenantId;
        if (!ambientTenantId.HasValue || ambientTenantId.Value == Guid.Empty)
            return normalized;

        if (BackupRunTenantSlugResolver.EncodesTenantScope(normalized))
            return normalized;

        return $"manual-tenant-{ambientTenantId.Value:D}-{DateTime.UtcNow.Ticks}";
    }

    private static async Task TryAcquirePostgresManualEnqueueSerializationAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true)
            return;

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({ManualEnqueueAdvisoryLockKey1}, {ManualEnqueueAdvisoryLockKey2})",
            cancellationToken);
    }

    private static bool IsBackupRunIdempotencyUniqueViolation(DbUpdateException ex)
    {
        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
        {
            if (inner is PostgresException pg
                && pg.SqlState == PostgresErrorCodes.UniqueViolation
                && pg.ConstraintName != null
                && pg.ConstraintName.Contains("idempotency", StringComparison.OrdinalIgnoreCase)
                && pg.ConstraintName.Contains("backup_runs", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
