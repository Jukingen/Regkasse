using System.Text.Json;
using System.Text.Json.Nodes;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.OperationalRuns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Dequeues <see cref="BackupRunStatus.Queued"/> rows and executes backup + artifact metadata verification off the HTTP pipeline.
/// </summary>
/// <remarks>
/// Çoklu örnek: varsayılan olarak <see cref="IBackupOrchestratorDistributedLock"/> PostgreSQL oturum advisory lock kullanır
/// (<c>Backup:OrchestratorDistributedLockEnabled</c>). Kilit yoksa veya DB hatası varsa bu tick dequeue edilmez (yanlış başarı yok).
/// </remarks>
public sealed class BackupOrchestratorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackupOrchestratorDistributedLock _distributedLock;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly FakeBackupExecutionAdapter _fakeAdapter;
    private readonly PostgreSqlBackupExecutionAdapterStub _productionStubAdapter;
    private readonly PostgreSqlPgDumpBackupExecutionAdapter _pgDumpAdapter;
    private readonly IBackupArtifactExternalArchive _externalArchive;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IBackupAlertPublisher _alerts;
    private readonly IBackupOrchestratorMetrics _orchestratorMetrics;
    private readonly ILogger<BackupOrchestratorHostedService> _logger;

    public BackupOrchestratorHostedService(
        IServiceScopeFactory scopeFactory,
        IBackupOrchestratorDistributedLock distributedLock,
        IOptionsMonitor<BackupOptions> options,
        FakeBackupExecutionAdapter fakeAdapter,
        PostgreSqlBackupExecutionAdapterStub productionStubAdapter,
        PostgreSqlPgDumpBackupExecutionAdapter pgDumpAdapter,
        IBackupArtifactExternalArchive externalArchive,
        IHostEnvironment hostEnvironment,
        IBackupAlertPublisher alerts,
        IBackupOrchestratorMetrics orchestratorMetrics,
        ILogger<BackupOrchestratorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _distributedLock = distributedLock;
        _options = options;
        _fakeAdapter = fakeAdapter;
        _productionStubAdapter = productionStubAdapter;
        _pgDumpAdapter = pgDumpAdapter;
        _externalArchive = externalArchive;
        _hostEnvironment = hostEnvironment;
        _alerts = alerts;
        _orchestratorMetrics = orchestratorMetrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.CurrentValue.WorkerEnabled)
                    await ProcessNextIfAnyAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup orchestrator tick failed");
            }

            try
            {
                await Task.Delay(_options.CurrentValue.OrchestratorPollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private IBackupExecutionAdapter SelectAdapter()
    {
        return _options.CurrentValue.ExecutionAdapterKind switch
        {
            BackupExecutionAdapterKind.ProductionStub => _productionStubAdapter,
            BackupExecutionAdapterKind.PgDump => _pgDumpAdapter,
            _ => _fakeAdapter
        };
    }

    private async Task ProcessNextIfAnyAsync(CancellationToken ct)
    {
        var (attempt, lease) = await _distributedLock.TryEnterExclusiveAsync(ct);
        try
        {
            if (attempt == BackupOrchestratorGateAttempt.ContendedElsewhere)
                return;

            if (attempt == BackupOrchestratorGateAttempt.ConnectionFailed)
            {
                _logger.LogWarning(
                    "Backup orchestrator: distributed gate did not acquire lock (DB/config); dequeue skipped this tick — queued runs remain pending.");
                return;
            }

            await ProcessNextExclusiveBodyAsync(ct);
        }
        finally
        {
            if (lease != null)
                await lease.DisposeAsync();
        }
    }

    internal async Task ProcessNextExclusiveBodyAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verifier = scope.ServiceProvider.GetRequiredService<IBackupVerificationService>();

        var run = await db.BackupRuns
            .Where(r => r.Status == BackupRunStatus.Queued)
            .OrderBy(r => r.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (run == null)
            return;

        _logger.LogInformation(
            "Backup dequeue: runId={RunId}, correlationId={CorrelationId}, requestedAt={RequestedAt:o}",
            run.Id,
            run.CorrelationId,
            run.RequestedAt);

        run.Status = BackupRunStatus.Running;
        run.StartedAt = DateTime.UtcNow;
        run.ConfigSnapshotJson = OperationalRunConfigSnapshotBuilder.SerializeBackup(
            _options.CurrentValue,
            "backup_run_start",
            DateTime.UtcNow);
        var leaseOpts = _options.CurrentValue;
        RunLeaseHeartbeatHelper.StampInitialLease(run, DateTime.UtcNow, leaseOpts.RunLeaseTimeout);
        await db.SaveChangesAsync(ct);

        var runId = run.Id;
        var correlationId = run.CorrelationId;
        try
        {
            await RunLeaseHeartbeatHelper.RunWithBackupHeartbeatAsync(
                _scopeFactory,
                () => _options.CurrentValue.HeartbeatInterval,
                () => _options.CurrentValue.RunLeaseTimeout,
                runId,
                () => ExecuteBackupRunWorkAsync(db, verifier, run, ct),
                _logger,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await using var finalizeScope = _scopeFactory.CreateAsyncScope();
            var fdb = finalizeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await BackupOrchestratorRunFinalizer.TryFinalizeCancelledAsync(fdb, runId, _logger, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup orchestrator unhandled exception for run {RunId}", runId);
            await using var finalizeScope = _scopeFactory.CreateAsyncScope();
            var fdb = finalizeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await BackupOrchestratorRunFinalizer.TryFinalizeUnhandledExceptionAsync(fdb, runId, ex, _logger, ct);
            _alerts.Publish(new BackupAlertEvent(
                BackupAlertKind.BackupFailed,
                runId,
                correlationId,
                "Unhandled exception in backup orchestrator; run finalized if non-terminal.",
                new Dictionary<string, string> { ["failureStage"] = "unhandled_orchestrator_exception" }));
        }
        finally
        {
            await TryRecordBackupRunLifecycleMetricsAsync(runId, ct);
        }
    }

    private async Task TryRecordBackupRunLifecycleMetricsAsync(Guid runId, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var run = await db.BackupRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);
            if (run == null || !BackupOrchestratorRunFinalizer.IsTerminal(run.Status))
                return;

            var start = run.StartedAt ?? run.RequestedAt;
            var end = run.CompletedAt ?? DateTime.UtcNow;
            var seconds = Math.Max(0, (end - start).TotalSeconds);
            _orchestratorMetrics.RecordBackupRunCompleted(
                OperationalRunMetricLabels.FormatBackupRunStatus(run.Status),
                OperationalRunMetricLabels.BackupTrigger(run.TriggerSource),
                seconds);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Backup run lifecycle metrics recording failed for run {RunId}", runId);
        }
    }

    private async Task ExecuteBackupRunWorkAsync(
        AppDbContext db,
        IBackupVerificationService verifier,
        BackupRun run,
        CancellationToken ct)
    {
        var opts = _options.CurrentValue;
        var adapter = SelectAdapter();
            var execContext = new BackupExecutionContext(run.Id, run.CorrelationId, adapter.AdapterKind, ct);

            BackupExecutionResult result;
            try
            {
                result = await adapter.ExecuteAsync(execContext);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                run.Status = BackupRunStatus.Cancelled;
                run.CompletedAt = DateTime.UtcNow;
                run.FailureCode = "CANCELLED";
                run.FailureDetail = "Backup run cancelled before adapter completed.";
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup adapter threw for run {RunId}", run.Id);
                run.Status = BackupRunStatus.Failed;
                run.CompletedAt = DateTime.UtcNow;
                run.FailureCode = "UNHANDLED_EXCEPTION";
                run.FailureDetail = ex.Message;
                await db.SaveChangesAsync(ct);
                _alerts.Publish(new BackupAlertEvent(
                    BackupAlertKind.BackupFailed,
                    run.Id,
                    run.CorrelationId,
                    "Adapter threw before completion.",
                    new Dictionary<string, string> { ["failureStage"] = "unhandled_exception_adapter" }));
                return;
            }

            if (!result.Success)
            {
                if (string.Equals(result.ErrorCode, "PG_DUMP_CANCELLED", StringComparison.Ordinal))
                {
                    run.Status = BackupRunStatus.Cancelled;
                    run.CompletedAt = DateTime.UtcNow;
                    run.FailureCode = result.ErrorCode;
                    run.FailureDetail = result.ErrorDetail;
                    await db.SaveChangesAsync(ct);
                    return;
                }

                run.Status = BackupRunStatus.Failed;
                run.CompletedAt = DateTime.UtcNow;
                run.FailureCode = result.ErrorCode ?? "EXECUTION_FAILED";
                run.FailureDetail = result.ErrorDetail;
                await db.SaveChangesAsync(ct);
                _logger.LogWarning(
                    "Backup execution failed: runId={RunId}, adapterKind={AdapterKind}, code={Code}, detail={Detail}",
                    run.Id,
                    adapter.AdapterKind,
                    run.FailureCode,
                    run.FailureDetail);
                _alerts.Publish(new BackupAlertEvent(
                    BackupAlertKind.BackupFailed,
                    run.Id,
                    run.CorrelationId,
                    result.ErrorDetail ?? "Backup execution failed.",
                    new Dictionary<string, string>
                    {
                        ["errorCode"] = run.FailureCode ?? "",
                        ["adapterKind"] = adapter.AdapterKind
                    }));
                return;
            }

            var artifactEntities = new List<BackupArtifact>();
            foreach (var d in result.Artifacts)
            {
                var row = new BackupArtifact
                {
                    BackupRunId = run.Id,
                    ArtifactType = d.ArtifactType,
                    StorageDescriptor = d.StorageDescriptor,
                    ByteSize = d.ByteSize,
                    ContentHashSha256 = d.ContentHashSha256,
                    MetadataJson = d.MetadataJson,
                    CreatedAt = DateTime.UtcNow,
                    LifecycleState = BackupArtifactLifecycleState.Staging
                };
                artifactEntities.Add(row);
                db.BackupArtifacts.Add(row);
            }

            run.Status = BackupRunStatus.AwaitingVerification;
            var leaseNow = DateTime.UtcNow;
            run.LastHeartbeatAtUtc = leaseNow;
            run.LeaseExpiresAtUtc = leaseNow + opts.RunLeaseTimeout;
            await db.SaveChangesAsync(ct);

            var verification = new BackupVerification
            {
                BackupRunId = run.Id,
                Status = BackupVerificationStatus.Pending,
                StartedAt = DateTime.UtcNow,
                VerifierSource = "ArtifactMetadataVerifier.Phase3Pipeline",
                CompletenessFlag = false
            };
            db.BackupVerifications.Add(verification);
            await db.SaveChangesAsync(ct);

            BackupVerificationOutcome vOutcome;
            try
            {
                vOutcome = await verifier.VerifyArtifactsAsync(run.Id, result.Artifacts, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                run.Status = BackupRunStatus.Cancelled;
                run.CompletedAt = DateTime.UtcNow;
                run.FailureCode = "CANCELLED";
                run.FailureDetail = "Backup run cancelled during artifact verification.";
                verification.Status = BackupVerificationStatus.Failed;
                verification.CompletedAt = DateTime.UtcNow;
                verification.FailureReason = run.FailureDetail;
                verification.DetailsJson = JsonSerializer.Serialize(new { error = "cancelled_during_verification" });
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Verifier threw for run {RunId}", run.Id);
                verification.Status = BackupVerificationStatus.Failed;
                verification.CompletedAt = DateTime.UtcNow;
                verification.FailureReason = ex.Message;
                verification.DetailsJson = JsonSerializer.Serialize(new { error = "verifier_exception" });
                run.Status = BackupRunStatus.VerificationFailed;
                run.CompletedAt = DateTime.UtcNow;
                run.FailureCode = "VERIFIER_EXCEPTION";
                run.FailureDetail = ex.Message;
                await db.SaveChangesAsync(ct);
                _alerts.Publish(new BackupAlertEvent(
                    BackupAlertKind.VerificationFailed,
                    run.Id,
                    run.CorrelationId,
                    "Artifact metadata verifier threw.",
                    new Dictionary<string, string> { ["failureStage"] = "verifier_exception" }));
                return;
            }

            verification.CompletedAt = DateTime.UtcNow;
            verification.CompletenessFlag = vOutcome.CompletenessFlag;

            if (!vOutcome.Passed)
            {
                verification.Status = BackupVerificationStatus.Failed;
                verification.FailureReason = vOutcome.FailureReason;
                verification.DetailsJson = vOutcome.DetailsJson;
                run.Status = BackupRunStatus.VerificationFailed;
                run.CompletedAt = DateTime.UtcNow;
                run.FailureCode = "VERIFICATION_FAILED";
                run.FailureDetail = vOutcome.FailureReason;
                _logger.LogWarning(
                    "Backup artifact metadata verification failed: runId={RunId}, reason={Reason}",
                    run.Id,
                    vOutcome.FailureReason);
                _alerts.Publish(new BackupAlertEvent(
                    BackupAlertKind.VerificationFailed,
                    run.Id,
                    run.CorrelationId,
                    vOutcome.FailureReason ?? "Artifact metadata verification failed.",
                    new Dictionary<string, string> { ["failureStage"] = "artifact_metadata_verification" }));
                await db.SaveChangesAsync(ct);
                return;
            }

            var detailsRoot = string.IsNullOrWhiteSpace(vOutcome.DetailsJson)
                ? new JsonObject()
                : JsonNode.Parse(vOutcome.DetailsJson)!.AsObject();

            foreach (var row in artifactEntities)
            {
                row.LifecycleState = BackupArtifactLifecycleState.StagingVerified;
                row.MetadataJson = BackupArtifactMetadataExtensions.MergePipelineFragment(
                    row.MetadataJson,
                    new { stagingVerification = "passed" });
            }

            var adapterKind = opts.ExecutionAdapterKind;
            if (!BackupArtifactPipelinePolicyEvaluator.ShouldRunExternalArchiveAfterStagingVerification(
                    adapterKind,
                    _hostEnvironment,
                    opts))
            {
                detailsRoot["externalArchive"] = JsonSerializer.SerializeToNode(new
                {
                    skipped = true,
                    reason = "adapter_not_pg_dump_or_development_without_external_root"
                });
                verification.Status = BackupVerificationStatus.Passed;
                verification.FailureReason = null;
                verification.DetailsJson = detailsRoot.ToJsonString();
                run.Status = BackupRunStatus.Succeeded;
                run.CompletedAt = DateTime.UtcNow;
                run.FailureCode = null;
                run.FailureDetail = null;
                await db.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "Backup run succeeded (staging verification; external archive skipped): runId={RunId}, adapterKind={AdapterKind}",
                    run.Id,
                    adapter.AdapterKind);
                return;
            }

            var stagingRootFull = string.IsNullOrWhiteSpace(opts.ArtifactStagingRoot)
                ? null
                : Path.GetFullPath(opts.ArtifactStagingRoot.Trim());
            if (string.IsNullOrWhiteSpace(stagingRootFull))
            {
                foreach (var row in artifactEntities)
                {
                    row.LifecycleState = BackupArtifactLifecycleState.ExternalCopyFailed;
                    row.MetadataJson = BackupArtifactMetadataExtensions.MergePipelineFragment(
                        row.MetadataJson,
                        new { externalCopy = "failed", code = "MISSING_STAGING_ROOT" });
                }

                verification.Status = BackupVerificationStatus.Failed;
                verification.FailureReason = "External archive requires Backup:ArtifactStagingRoot.";
                detailsRoot["externalArchive"] = JsonSerializer.SerializeToNode(new { success = false, code = "MISSING_STAGING_ROOT" });
                verification.DetailsJson = detailsRoot.ToJsonString();
                run.Status = BackupRunStatus.VerificationFailed;
                run.CompletedAt = DateTime.UtcNow;
                run.FailureCode = "EXTERNAL_ARCHIVE_FAILED";
                run.FailureDetail = verification.FailureReason;
                await db.SaveChangesAsync(ct);
                _alerts.Publish(new BackupAlertEvent(
                    BackupAlertKind.VerificationFailed,
                    run.Id,
                    run.CorrelationId,
                    verification.FailureReason,
                    new Dictionary<string, string> { ["failureStage"] = "external_archive_precheck" }));
                return;
            }

            var extRoot = Path.GetFullPath(opts.ExternalArchiveRoot!.Trim());
            var extOutcome = await _externalArchive.CopyStagingArtifactsAsync(
                run.Id,
                stagingRootFull,
                extRoot,
                result.Artifacts,
                ct);

            if (!extOutcome.Success)
            {
                foreach (var row in artifactEntities)
                {
                    row.LifecycleState = BackupArtifactLifecycleState.ExternalCopyFailed;
                    row.MetadataJson = BackupArtifactMetadataExtensions.MergePipelineFragment(
                        row.MetadataJson,
                        new { externalCopy = "failed", code = extOutcome.ErrorCode });
                }

                verification.Status = BackupVerificationStatus.Failed;
                verification.FailureReason = extOutcome.ErrorDetail;
                detailsRoot["externalArchive"] = JsonSerializer.SerializeToNode(new
                {
                    success = false,
                    code = extOutcome.ErrorCode
                });
                verification.DetailsJson = detailsRoot.ToJsonString();
                run.Status = BackupRunStatus.VerificationFailed;
                run.CompletedAt = DateTime.UtcNow;
                run.FailureCode = "EXTERNAL_ARCHIVE_FAILED";
                run.FailureDetail = extOutcome.ErrorDetail;
                await db.SaveChangesAsync(ct);
                _logger.LogWarning(
                    "Backup external archive failed: runId={RunId}, code={Code}, detail={Detail}",
                    run.Id,
                    extOutcome.ErrorCode,
                    extOutcome.ErrorDetail);
                _alerts.Publish(new BackupAlertEvent(
                    BackupAlertKind.VerificationFailed,
                    run.Id,
                    run.CorrelationId,
                    extOutcome.ErrorDetail ?? "External archive failed.",
                    new Dictionary<string, string>
                    {
                        ["failureStage"] = "external_archive_copy",
                        ["errorCode"] = extOutcome.ErrorCode ?? ""
                    }));
                return;
            }

            foreach (var row in artifactEntities)
            {
                row.LifecycleState = BackupArtifactLifecycleState.ExternalCopyVerified;
                if (extOutcome.RedactedLocators.TryGetValue(row.ArtifactType, out var loc))
                {
                    row.ExternalRedactedLocator = loc;
                    row.MetadataJson = BackupArtifactMetadataExtensions.MergePipelineFragment(
                        row.MetadataJson,
                        new { externalCopy = "verified", redactedLocator = loc });
                }
            }

            detailsRoot["externalArchive"] = JsonSerializer.SerializeToNode(new
            {
                success = true,
                postCopyHashVerified = true
            });
            verification.Status = BackupVerificationStatus.Passed;
            verification.FailureReason = null;
            verification.DetailsJson = detailsRoot.ToJsonString();
            run.Status = BackupRunStatus.Succeeded;
            run.CompletedAt = DateTime.UtcNow;
            run.FailureCode = null;
            run.FailureDetail = null;
            await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Backup run succeeded after staging + external archive verification: runId={RunId}, adapterKind={AdapterKind}",
            run.Id,
            adapter.AdapterKind);
    }
}
