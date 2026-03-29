using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.OperationalRuns;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Restore drill: dump TOC incelemesi (<c>pg_restore --list</c>), isteğe bağlı izole <c>pg_restore</c>, isteğe bağlı fiscal SQL (ayrı bağlantı), isteğe bağlı canlı integrity (read-only).
/// </summary>
/// <remarks>
/// Çoklu örnek: <see cref="IRestoreVerificationOrchestratorDistributedLock"/> ile Backup worker’dan bağımsız PostgreSQL advisory lock
/// (<c>RestoreVerification:OrchestratorDistributedLockEnabled</c>). Kilit yoksa veya DB hatası varsa tick atlanır; Queued satırına yanlış başarı yazılmaz.
/// </remarks>
public sealed class RestoreVerificationOrchestratorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<RestoreVerificationOptions> _restoreOpts;
    private readonly IOptionsMonitor<BackupOptions> _backupOpts;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly IRestoreVerificationOrchestratorDistributedLock _distributedLock;
    private readonly IPgRestoreListInspector _pgRestoreList;
    private readonly IPgRestoreIsolatedRestoreRunner _isolatedRestore;
    private readonly IFiscalGoLiveValidationRunner _fiscalRunner;
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
        IFiscalGoLiveValidationRunner fiscalRunner,
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
        _fiscalRunner = fiscalRunner;
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

    private async Task ExecuteRestoreVerificationRunWorkAsync(
        IServiceProvider sp,
        AppDbContext db,
        RestoreVerificationRun run,
        CancellationToken ct)
    {
        var backupOpts = _backupOpts.CurrentValue;
            var restoreOpts = _restoreOpts.CurrentValue;
            var details = new JsonObject
            {
                ["scope"] = "restore_confidence_drill_not_artifact_checksum",
                ["tseRestoreVerification"] = "deferred_vendor_scope",
                ["finanzOnlineOutbox"] =
                    "Restore drill does not replay FinanzOnline outbox; treat as separate operational concern.",
                ["integrityInterpretation"] =
                    "When IncludeLiveIntegrityChecks runs against DefaultConnection, it validates current operational data only; post-restore fiscal/schema checks require fiscal script on a restored clone connection."
            };

            var fallbackDepth = restoreOpts.DumpFallbackDepth;
            var backupRunQuery = sp.GetRequiredService<IBackupRunQueryService>();
            var candidateRunIds = await backupRunQuery.GetRecentSucceededRunIdsAsync(fallbackDepth, ct);
            var dump = await RestoreVerificationDumpPathResolver.TryResolveAmongSucceededCandidatesAsync(
                db,
                backupOpts,
                candidateRunIds,
                _logger,
                ct);

            if (dump == null)
            {
                details["dumpResolution"] = JsonSerializer.SerializeToNode(new
                {
                    outcome = "no_dump_available",
                    fallbackDepth,
                    candidateRunCount = candidateRunIds.Count
                });
                run.Status = RestoreVerificationStatus.Failed;
                run.CompletedAt = DateTime.UtcNow;
                run.FailureCode = "NO_DUMP_AVAILABLE";
                run.FailureDetail =
                    $"No logical dump file found on disk for the {fallbackDepth} most recent successful backup run(s) (checked staging, then external archive per run).";
                run.DetailsJson = details.ToJsonString();
                await db.SaveChangesAsync(ct);
                return;
            }

            run.SourceBackupRunId = dump.Value.backupRunId;
            run.DumpRelativeDescriptor = dump.Value.relativeDescriptor;

            var listResult = await _pgRestoreList.InspectDumpFileAsync(dump.Value.absolutePath, ct);
            run.PgRestoreListExitCode = listResult.ExitCode;
            run.PgRestoreListLineCount = listResult.NonEmptyLineCount;
            run.PgRestoreListPassed = listResult.Success;
            var inspectionNode = JsonSerializer.SerializeToNode(new
            {
                passed = listResult.Success,
                exitCode = listResult.ExitCode,
                lineCount = listResult.NonEmptyLineCount,
                kind = "pg_restore_list_toc_inspection_not_checksum"
            });
            details["pgRestoreList"] = inspectionNode;
            details["dumpInspection"] = inspectionNode;

            if (!listResult.Success)
            {
                run.Status = RestoreVerificationStatus.Failed;
                run.CompletedAt = DateTime.UtcNow;
                run.FailureCode = "PG_RESTORE_LIST_FAILED";
                run.FailureDetail = listResult.StdErrSnippet ?? "pg_restore --list failed.";
                run.DetailsJson = details.ToJsonString();
                await db.SaveChangesAsync(ct);
                return;
            }

            var restoreAttemptNode = new JsonObject();
            details["restoreAttempt"] = restoreAttemptNode;
            var isoEnabled = restoreOpts.IsolatedPgRestoreEnabled;
            var isoConnName = restoreOpts.IsolatedRestoreAdminConnectionStringName;
            if (!isoEnabled)
            {
                run.RestoreAttemptExecuted = false;
                run.RestoreAttemptPassed = null;
                run.RestoreAttemptExitCode = null;
                run.RestoreAttemptSkipReason = "ISOLATED_PG_RESTORE_DISABLED";
                run.RestoreTargetDbRedacted = null;
                restoreAttemptNode["skipped"] = true;
                restoreAttemptNode["reason"] = run.RestoreAttemptSkipReason;
            }
            else if (_hostEnvironment.IsProduction()
                     && string.Equals(isoConnName?.Trim(), "DefaultConnection", StringComparison.OrdinalIgnoreCase))
            {
                run.RestoreAttemptExecuted = false;
                run.RestoreAttemptPassed = null;
                run.RestoreAttemptExitCode = null;
                run.RestoreAttemptSkipReason = "PRODUCTION_REQUIRES_NON_DEFAULT_CONNECTION";
                run.RestoreTargetDbRedacted = null;
                restoreAttemptNode["skipped"] = true;
                restoreAttemptNode["reason"] = run.RestoreAttemptSkipReason;
                run.Status = RestoreVerificationStatus.Failed;
                run.CompletedAt = DateTime.UtcNow;
                run.FailureCode = "RESTORE_ATTEMPT_NOT_ALLOWED";
                run.FailureDetail =
                    "Isolated restore admin connection must not be DefaultConnection in Production.";
                run.DetailsJson = details.ToJsonString();
                await db.SaveChangesAsync(ct);
                return;
            }
            else
            {
                var adminCs = _configuration.GetConnectionString(isoConnName!.Trim());
                if (string.IsNullOrWhiteSpace(adminCs))
                {
                    run.RestoreAttemptExecuted = false;
                    run.RestoreAttemptPassed = null;
                    run.RestoreAttemptExitCode = null;
                    run.RestoreAttemptSkipReason = "MISSING_ADMIN_CONNECTION_STRING";
                    run.RestoreTargetDbRedacted = null;
                    restoreAttemptNode["skipped"] = false;
                    restoreAttemptNode["executed"] = false;
                    restoreAttemptNode["reason"] = run.RestoreAttemptSkipReason;
                    run.Status = RestoreVerificationStatus.Failed;
                    run.CompletedAt = DateTime.UtcNow;
                    run.FailureCode = "ISOLATED_RESTORE_NOT_CONFIGURED";
                    run.FailureDetail =
                        "IsolatedPgRestoreEnabled is true but IsolatedRestoreAdminConnectionStringName is missing or empty in configuration.";
                    run.DetailsJson = details.ToJsonString();
                    await db.SaveChangesAsync(ct);
                    return;
                }

                var dbName = $"rv_v_{run.Id:N}";
                run.RestoreTargetDbRedacted = dbName;
                var timeoutSec = restoreOpts.IsolatedPgRestoreTimeoutSeconds <= 0
                    ? 3600
                    : restoreOpts.IsolatedPgRestoreTimeoutSeconds;
                var timeout = TimeSpan.FromSeconds(Math.Max(60, timeoutSec));

                _logger.LogInformation(
                    "Restore verification isolated pg_restore: runId={RunId}, backupRunId={BackupRunId}, targetDb={TargetDb}",
                    run.Id,
                    run.SourceBackupRunId,
                    dbName);

                PgRestoreIsolatedRestoreOutcome isolatedOutcome;
                try
                {
                    isolatedOutcome = await _isolatedRestore.RestoreCustomDumpToEphemeralDatabaseAsync(
                        adminCs,
                        dump.Value.absolutePath,
                        dbName,
                        restoreOpts.PgRestoreExecutablePath,
                        timeout,
                        ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    run.RestoreAttemptExecuted = true;
                    run.RestoreAttemptPassed = false;
                    run.RestoreAttemptExitCode = -3;
                    run.RestoreAttemptSkipReason = null;
                    restoreAttemptNode["executed"] = true;
                    restoreAttemptNode["passed"] = false;
                    restoreAttemptNode["cancelled"] = true;
                    run.Status = RestoreVerificationStatus.Failed;
                    run.CompletedAt = DateTime.UtcNow;
                    run.FailureCode = "ISOLATED_PG_RESTORE_CANCELLED";
                    run.FailureDetail = "Restore verification cancelled during isolated pg_restore.";
                    run.DetailsJson = details.ToJsonString();
                    await db.SaveChangesAsync(ct);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Isolated pg_restore runner threw for run {RunId}", run.Id);
                    run.RestoreAttemptExecuted = true;
                    run.RestoreAttemptPassed = false;
                    run.RestoreAttemptExitCode = -1;
                    restoreAttemptNode["executed"] = true;
                    restoreAttemptNode["passed"] = false;
                    restoreAttemptNode["error"] = ex.Message;
                    run.Status = RestoreVerificationStatus.Failed;
                    run.CompletedAt = DateTime.UtcNow;
                    run.FailureCode = "ISOLATED_PG_RESTORE_EXCEPTION";
                    run.FailureDetail = ex.Message;
                    run.DetailsJson = details.ToJsonString();
                    await db.SaveChangesAsync(ct);
                    return;
                }

                run.RestoreAttemptExecuted = true;
                run.RestoreAttemptPassed = isolatedOutcome.Success;
                run.RestoreAttemptExitCode = isolatedOutcome.ExitCode;
                run.RestoreAttemptSkipReason = null;
                restoreAttemptNode["executed"] = true;
                restoreAttemptNode["passed"] = isolatedOutcome.Success;
                restoreAttemptNode["exitCode"] = isolatedOutcome.ExitCode;
                restoreAttemptNode["targetDbRedacted"] = dbName;

                if (!isolatedOutcome.Success)
                {
                    run.Status = RestoreVerificationStatus.Failed;
                    run.CompletedAt = DateTime.UtcNow;
                    run.FailureCode = "ISOLATED_PG_RESTORE_FAILED";
                    run.FailureDetail = isolatedOutcome.StdErrSnippet ?? "pg_restore into ephemeral database failed.";
                    run.DetailsJson = details.ToJsonString();
                    await db.SaveChangesAsync(ct);
                    return;
                }

                restoreAttemptNode["ephemeralDbDropped"] = true;
                restoreAttemptNode["fiscalNote"] =
                    "Fiscal SQL does not automatically run against the ephemeral DB; use FiscalValidationConnectionStringName for a clone if post-restore fiscal checks are required.";
            }

            // Fiscal SQL: isolated connection only; never DefaultConnection in Production.
            var fiscalName = restoreOpts.FiscalValidationConnectionStringName;
            if (string.IsNullOrWhiteSpace(fiscalName))
            {
                run.FiscalSqlSkipped = true;
                run.FiscalSqlSkipReason = "FISCAL_CONNECTION_NOT_CONFIGURED";
                details["fiscalSql"] = JsonSerializer.SerializeToNode(new { skipped = true, reason = run.FiscalSqlSkipReason });
            }
            else if (_hostEnvironment.IsProduction()
                     && string.Equals(fiscalName.Trim(), "DefaultConnection", StringComparison.OrdinalIgnoreCase))
            {
                run.FiscalSqlSkipped = true;
                run.FiscalSqlSkipReason = "PRODUCTION_REQUIRES_NON_DEFAULT_CONNECTION";
                details["fiscalSql"] = JsonSerializer.SerializeToNode(new { skipped = true, reason = run.FiscalSqlSkipReason });
            }
            else
            {
                var fiscalCs = _configuration.GetConnectionString(fiscalName.Trim());
                if (string.IsNullOrWhiteSpace(fiscalCs))
                {
                    run.FiscalSqlSkipped = false;
                    run.FiscalSqlPassed = null;
                    details["fiscalSql"] = JsonSerializer.SerializeToNode(new
                    {
                        skipped = false,
                        executed = false,
                        reason = "MISSING_CONNECTION_STRING",
                        connectionName = fiscalName.Trim()
                    });
                    run.Status = RestoreVerificationStatus.Failed;
                    run.CompletedAt = DateTime.UtcNow;
                    run.FailureCode = "FISCAL_VALIDATION_NOT_EXECUTED";
                    run.FailureDetail =
                        "FiscalValidationConnectionStringName is set but the connection string entry is missing.";
                    run.DetailsJson = details.ToJsonString();
                    await db.SaveChangesAsync(ct);
                    return;
                }

                var contentRoot = _hostEnvironment.ContentRootPath;
                var scriptRel = restoreOpts.FiscalValidationScriptRelativePath;
                var scriptPath = Path.GetFullPath(Path.Combine(contentRoot, scriptRel));
                var fiscalOutcome = await _fiscalRunner.RunScriptAsync(scriptPath, fiscalCs, ct);
                if (!fiscalOutcome.Executed)
                {
                    run.FiscalSqlSkipped = true;
                    run.FiscalSqlSkipReason = string.IsNullOrWhiteSpace(fiscalOutcome.ErrorDetail)
                        ? "FISCAL_RUN_PREREQ_FAILED"
                        : fiscalOutcome.ErrorDetail;
                    details["fiscalSql"] = JsonSerializer.SerializeToNode(new
                    {
                        skipped = true,
                        executed = false,
                        error = fiscalOutcome.ErrorDetail
                    });
                    run.Status = RestoreVerificationStatus.Failed;
                    run.CompletedAt = DateTime.UtcNow;
                    run.FailureCode = "FISCAL_VALIDATION_NOT_EXECUTED";
                    run.FailureDetail = fiscalOutcome.ErrorDetail ?? "Fiscal validation could not run.";
                    run.DetailsJson = details.ToJsonString();
                    await db.SaveChangesAsync(ct);
                    return;
                }

                run.FiscalSqlSkipped = false;
                run.FiscalSqlPassed = fiscalOutcome.Passed;
                run.FiscalSqlFailCount = fiscalOutcome.FailCount;
                run.FiscalSqlWarnCount = fiscalOutcome.WarnCount;
                details["fiscalSql"] = JsonSerializer.SerializeToNode(new
                {
                    executed = true,
                    passed = fiscalOutcome.Passed,
                    failCount = fiscalOutcome.FailCount,
                    warnCount = fiscalOutcome.WarnCount,
                    summary = fiscalOutcome.SummaryLine,
                    scriptPathRelative = scriptRel
                });

                if (!fiscalOutcome.Passed)
                {
                    run.Status = RestoreVerificationStatus.Failed;
                    run.CompletedAt = DateTime.UtcNow;
                    run.FailureCode = "FISCAL_VALIDATION_FAILED";
                    run.FailureDetail = fiscalOutcome.ErrorDetail ?? fiscalOutcome.SummaryLine ?? "Fiscal SQL reported FAIL rows.";
                    run.DetailsJson = details.ToJsonString();
                    await db.SaveChangesAsync(ct);
                    return;
                }
            }

            if (restoreOpts.IncludeLiveIntegrityChecks)
            {
                var integrity = sp.GetRequiredService<IIntegrityCheckService>();
                var toUtc = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
                var fromUtc = toUtc.AddDays(-restoreOpts.IntegrityLookbackDays);
                var report = await integrity.GetReportAsync(fromUtc, toUtc, includeDetails: false);
                var pass = report.SequenceIssues.DuplicateReceiptNumberCount == 0
                           && report.SequenceIssues.NonMonotonicSequenceCount == 0
                           && report.OrphanRefunds.OrphanRefundCount == 0
                           && report.PaymentWithoutInvoice.Count == 0;
                run.IntegrityScope = "LiveOperationalReadOnly";
                run.IntegrityChecksPassed = pass;
                details["integrity"] = JsonSerializer.SerializeToNode(new
                {
                    scope = run.IntegrityScope,
                    passed = pass,
                    duplicateReceiptNumbers = report.SequenceIssues.DuplicateReceiptNumberCount,
                    nonMonotonic = report.SequenceIssues.NonMonotonicSequenceCount,
                    orphanRefunds = report.OrphanRefunds.OrphanRefundCount,
                    paymentWithoutInvoice = report.PaymentWithoutInvoice.Count
                });

                if (!pass)
                {
                    run.Status = RestoreVerificationStatus.Failed;
                    run.CompletedAt = DateTime.UtcNow;
                    run.FailureCode = "INTEGRITY_CHECKS_FAILED";
                    run.FailureDetail = "Operational integrity report reported issues in lookback window.";
                    run.DetailsJson = details.ToJsonString();
                    await db.SaveChangesAsync(ct);
                    return;
                }
            }
            else
            {
                run.IntegrityChecksPassed = null;
                details["integrity"] = JsonSerializer.SerializeToNode(new { skipped = true });
            }

            run.Status = RestoreVerificationStatus.Succeeded;
            run.CompletedAt = DateTime.UtcNow;
            run.FailureCode = null;
            run.FailureDetail = null;
            run.DetailsJson = details.ToJsonString();
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Restore verification drill succeeded: runId={RunId}, backupRunId={BackupRunId}",
                run.Id,
                run.SourceBackupRunId);
    }
}
