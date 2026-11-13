using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.OperationalRuns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Restore drill: dump TOC incelemesi (<c>pg_restore --list</c>), isteğe bağlı izole <c>pg_restore</c>, isteğe bağlı fiscal SQL (ayrı bağlantı), isteğe bağlı canlı integrity (read-only).
/// </summary>
/// <remarks>
/// Çoklu örnek: <see cref="IRestoreVerificationOrchestratorDistributedLock"/> ile Backup worker’dan bağımsız PostgreSQL advisory lock
/// (<c>RestoreVerification:OrchestratorDistributedLockEnabled</c>). Kilit yoksa veya DB hatası varsa tick atlanır; Queued satırına yanlış başarı yazılmaz.
/// </remarks>
public sealed partial class RestoreVerificationOrchestratorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<RestoreVerificationOptions> _restoreOpts;
    private readonly IOptionsMonitor<BackupOptions> _backupOpts;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly IRestoreVerificationOrchestratorDistributedLock _distributedLock;
    private readonly IPgRestoreListInspector _pgRestoreList;
    private readonly IPgRestoreIsolatedRestoreRunner _isolatedRestore;
    private readonly IPostRestoreDrillSqlChecker _postRestoreSqlChecker;
    private readonly IRestoredDatabaseApplicationSmokeRunner _restoredDatabaseApplicationSmoke;
    private readonly IFiscalGoLiveValidationRunner _fiscalRunner;
    private readonly IApplicationRecoverySmokeProbe _applicationSmokeProbe;
    private readonly IExternalDependencyRecoveryEvidenceBuilder _externalDependencyEvidence;
    private readonly IRestoreVerificationOperationalReadiness _restoreReadiness;
    private readonly IRestoreVerificationOrchestratorMetrics _orchestratorMetrics;
    private readonly IBackupAlertPublisher _alerts;
    private readonly ILogger<RestoreVerificationOrchestratorHostedService> _logger;

    public RestoreVerificationOrchestratorHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<RestoreVerificationOptions> restoreOpts,
        IOptionsMonitor<BackupOptions> backupOpts,
        IHostEnvironment hostEnvironment,
        IConfiguration configuration,
        IRestoreVerificationOrchestratorDistributedLock distributedLock,
        IPgRestoreListInspector pgRestoreList,
        IPgRestoreIsolatedRestoreRunner isolatedRestore,
        IPostRestoreDrillSqlChecker postRestoreSqlChecker,
        IRestoredDatabaseApplicationSmokeRunner restoredDatabaseApplicationSmoke,
        IFiscalGoLiveValidationRunner fiscalRunner,
        IApplicationRecoverySmokeProbe applicationSmokeProbe,
        IExternalDependencyRecoveryEvidenceBuilder externalDependencyEvidence,
        IRestoreVerificationOperationalReadiness restoreReadiness,
        IRestoreVerificationOrchestratorMetrics orchestratorMetrics,
        IBackupAlertPublisher alerts,
        ILogger<RestoreVerificationOrchestratorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _restoreOpts = restoreOpts;
        _backupOpts = backupOpts;
        _hostEnvironment = hostEnvironment;
        _configuration = configuration;
        _distributedLock = distributedLock;
        _pgRestoreList = pgRestoreList;
        _isolatedRestore = isolatedRestore;
        _postRestoreSqlChecker = postRestoreSqlChecker;
        _restoredDatabaseApplicationSmoke = restoredDatabaseApplicationSmoke;
        _fiscalRunner = fiscalRunner;
        _applicationSmokeProbe = applicationSmokeProbe;
        _externalDependencyEvidence = externalDependencyEvidence;
        _restoreReadiness = restoreReadiness;
        _orchestratorMetrics = orchestratorMetrics;
        _alerts = alerts;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ro = _restoreOpts.CurrentValue;
                if (ro.WorkerEnabled)
                    await ProcessOneTickWithDistributedGateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Restore verification orchestrator tick failed");
            }

            try
            {
                await Task.Delay(_restoreOpts.CurrentValue.OrchestratorPollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessOneTickWithDistributedGateAsync(CancellationToken ct)
    {
        var (attempt, lease) = await _distributedLock.TryEnterExclusiveAsync(ct);
        try
        {
            if (attempt == RestoreVerificationOrchestratorGateAttempt.ContendedElsewhere)
                return;

            if (attempt == RestoreVerificationOrchestratorGateAttempt.ConnectionFailed)
            {
                _orchestratorMetrics.RecordWorkerTickSuppressed("distributed_gate_connection_failed");
                _logger.LogWarning(
                    "Restore verification orchestrator: distributed gate did not acquire lock (DB/config); tick skipped — queued drills remain pending.");
                return;
            }

            await TryEnqueueWeeklyIfDueExclusiveBodyAsync(ct);
            await ProcessNextExclusiveBodyAsync(ct);
        }
        finally
        {
            if (lease != null)
                await lease.DisposeAsync();
        }
    }

    /// <summary>
    /// Zamanlanmış drill: son başarılı <em>zamanlanmış</em> kanıt (CompletedAt) ve aktif zamanlanmış Queued/Running durumuna göre sıraya alınır.
    /// </summary>
    internal async Task TryEnqueueWeeklyIfDueExclusiveBodyAsync(CancellationToken ct)
    {
        var ro = _restoreOpts.CurrentValue;
        if (!ro.ScheduledWeeklyDrillEnabled)
            return;

        var health = _restoreReadiness.GetConfigurationHealth();
        if (health.Level == RestoreVerificationConfigurationHealthLevel.Unhealthy)
        {
            _orchestratorMetrics.RecordScheduledEnqueueSuppressed("unhealthy_configuration");
            _logger.LogWarning(
                "Scheduled weekly restore verification enqueue skipped: configuration health is Unhealthy.");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var queries = sp.GetRequiredService<IRestoreVerificationSchedulingQueryService>();

        if (await queries.HasActiveScheduledQueuedOrRunningAsync(ct))
            return;

        // Cadence: yalnızca zamanlanmış (Scheduled) ve terminal başarılı kanıtın CompletedAt değeri; RequestedAt veya manuel başarılar dikkate alınmaz.
        var lastProofUtc = await queries.GetLastSuccessfulScheduledProofCompletedAtUtcAsync(ct);

        var windowStart = DateTime.UtcNow.AddDays(-ro.ScheduledProofCadenceDays);
        if (lastProofUtc.HasValue && lastProofUtc.Value > windowStart)
            return;

        var db = sp.GetRequiredService<AppDbContext>();
        var capturedAt = DateTime.UtcNow;
        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Queued,
            TriggerSource = RestoreVerificationTriggerSource.Scheduled,
            RequestedAt = capturedAt,
            ConfigSnapshotJson = OperationalRunConfigSnapshotBuilder.SerializeRestore(
                ro,
                "restore_scheduled_enqueue",
                capturedAt)
        });
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Enqueued scheduled weekly restore verification drill.");
    }

    internal async Task ProcessNextExclusiveBodyAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var run = await db.RestoreVerificationRuns
            .Where(r => r.Status == RestoreVerificationStatus.Queued)
            .OrderBy(r => r.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (run == null)
            return;

        run.Status = RestoreVerificationStatus.Running;
        run.StartedAt = DateTime.UtcNow;
        var rvLease = _restoreOpts.CurrentValue;
        run.ConfigSnapshotJson = OperationalRunConfigSnapshotBuilder.SerializeRestore(
            rvLease,
            "restore_run_start",
            DateTime.UtcNow);
        RunLeaseHeartbeatHelper.StampInitialLease(run, DateTime.UtcNow, rvLease.RunLeaseTimeout);
        await db.SaveChangesAsync(ct);

        var runId = run.Id;
        try
        {
            await RunLeaseHeartbeatHelper.RunWithRestoreHeartbeatAsync(
                _scopeFactory,
                () => _restoreOpts.CurrentValue.HeartbeatInterval,
                () => _restoreOpts.CurrentValue.RunLeaseTimeout,
                runId,
                () => ExecuteRestoreVerificationRunWorkAsync(sp, db, run, ct),
                _logger,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await using var finalizeScope = _scopeFactory.CreateAsyncScope();
            var fdb = finalizeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await RestoreVerificationOrchestratorRunFinalizer.TryFinalizeCancelledAsync(fdb, runId, _logger, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore verification orchestrator unhandled exception for run {RunId}", runId);
            await using var finalizeScope = _scopeFactory.CreateAsyncScope();
            var fdb = finalizeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await RestoreVerificationOrchestratorRunFinalizer.TryFinalizeUnhandledExceptionAsync(fdb, runId, ex, _logger, ct);
        }
        finally
        {
            await TryRecordRestoreVerificationRunMetricsAsync(runId, ct);
            await TryPublishRestoreVerificationFailureAlertAsync(runId, ct);
        }
    }

    private async Task TryRecordRestoreVerificationRunMetricsAsync(Guid runId, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var run = await db.RestoreVerificationRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);
            if (run == null || !RestoreVerificationOrchestratorRunFinalizer.IsTerminal(run.Status))
                return;

            var start = run.StartedAt ?? run.RequestedAt;
            var end = run.CompletedAt ?? DateTime.UtcNow;
            var seconds = Math.Max(0, (end - start).TotalSeconds);
            _orchestratorMetrics.RecordRestoreVerificationRunCompleted(
                OperationalRunMetricLabels.FormatRestoreVerificationStatus(run.Status),
                OperationalRunMetricLabels.RestoreTrigger(run.TriggerSource),
                seconds);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Restore verification lifecycle metrics recording failed for run {RunId}", runId);
        }
    }

    private async Task TryPublishRestoreVerificationFailureAlertAsync(Guid runId, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var run = await db.RestoreVerificationRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);
            if (run == null
                || run.Status != RestoreVerificationStatus.Failed
                || run.StaleRecoveredAtUtc != null)
                return;

            var data = new Dictionary<string, string>
            {
                ["failureCode"] = run.FailureCode ?? "",
                ["triggerSource"] = OperationalRunMetricLabels.RestoreTrigger(run.TriggerSource)
            };
            if (string.Equals(run.FailureCode, "UNHANDLED_EXCEPTION", StringComparison.Ordinal))
                data["failureStage"] = "unhandled_orchestrator_exception";

            _alerts.Publish(new BackupAlertEvent(
                BackupAlertKind.RestoreVerificationFailed,
                run.SourceBackupRunId,
                run.CorrelationId,
                run.FailureDetail ?? "Restore verification drill failed.",
                data,
                run.Id));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Restore verification failure alert hook failed for run {RunId}", runId);
        }
    }

}
