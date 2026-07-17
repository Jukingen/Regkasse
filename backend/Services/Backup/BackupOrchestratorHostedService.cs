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
    private readonly TenantScopedLogicalBackupExecutionAdapter _tenantLogicalAdapter;
    private readonly CompositeSystemBackupExecutionAdapter _systemCompositeAdapter;
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
        TenantScopedLogicalBackupExecutionAdapter tenantLogicalAdapter,
        CompositeSystemBackupExecutionAdapter systemCompositeAdapter,
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
        _tenantLogicalAdapter = tenantLogicalAdapter;
        _systemCompositeAdapter = systemCompositeAdapter;
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

    private IBackupExecutionAdapter SelectAdapter(BackupExecutionAdapterKind kind, BackupStrategyKind strategy)
    {
        // Tenant → tenant JSON ZIP; System → pg_dump + structured system.zip (composite).
        // Fake / ProductionStub keep pipeline-test adapters.
        if (kind == BackupExecutionAdapterKind.PgDump)
        {
            if (strategy == BackupStrategyKind.Tenant)
                return _tenantLogicalAdapter;
            if (strategy == BackupStrategyKind.System)
                return _systemCompositeAdapter;
        }

        return kind switch
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

            await TryProcessAutomaticRetriesUnderLockAsync(ct);
            await ProcessNextExclusiveBodyAsync(ct);
        }
        finally
        {
            if (lease != null)
                await lease.DisposeAsync();
        }
    }

    private async Task TryProcessAutomaticRetriesUnderLockAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await BackupAutomaticRetryCoordinator.TryProcessOneDueAutomaticRetryAsync(
            db,
            _options.CurrentValue,
            DateTime.UtcNow,
            _logger,
            ct);
    }

    internal async Task ProcessNextExclusiveBodyAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verifier = scope.ServiceProvider.GetRequiredService<IBackupVerificationService>();
        var postSuccess = scope.ServiceProvider.GetRequiredService<IBackupPostSuccessOrchestrationHook>();

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
            DateTime.UtcNow,
            backupStrategy: run.Strategy);
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
                () => ExecuteBackupRunWorkAsync(db, verifier, run, postSuccess, ct),
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
        IBackupPostSuccessOrchestrationHook postSuccess,
        CancellationToken ct)
    {
        var opts = _options.CurrentValue;
        var prefExec = await db.BackupRuntimeExecutionPreferences.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == BackupRuntimeExecutionPreference.SingletonId, ct);
        var adminModeExec = prefExec?.Mode ?? AdminBackupRuntimeExecutionMode.InheritFromConfiguration;
        var effectiveKindExec = BackupEffectiveExecutionAdapterResolver.ResolveEffectiveAdapterKind(opts, adminModeExec);
        var adapter = SelectAdapter(effectiveKindExec, run.Strategy);
            run.AdapterKind = adapter.AdapterKind;
            var tenantSlugForFileName = await BackupRunTenantSlugResolver.ResolveSlugAsync(run, db, ct);
            var artifactFileNameTimestampUtc = DateTime.UtcNow;
            DateTime? incrementalSinceUtc = null;
            if (BackupIncrementalPackageMetadata.TryReadIncrementalSinceUtc(run.ConfigSnapshotJson, out var sinceUtc))
                incrementalSinceUtc = sinceUtc;

            var execContext = new BackupExecutionContext(
                run.Id,
                run.CorrelationId,
                adapter.AdapterKind,
                ct,
                tenantSlugForFileName,
                artifactFileNameTimestampUtc,
                run.Strategy,
                run.TenantId,
                incrementalSinceUtc);

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
                BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
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
                BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
                await db.SaveChangesAsync(ct);
                await BackupAutomaticRetryCoordinator.TrySchedulePendingRetryAfterTerminalSaveAsync(
                    db, run, opts, DateTime.UtcNow, _logger, ct);
                _alerts.Publish(new BackupAlertEvent(
                    BackupAlertKind.BackupFailed,
                    run.Id,
                    run.CorrelationId,
                    "Adapter threw before completion.",
                    new Dictionary<string, string>
                    {
                        ["failureStage"] = "unhandled_exception_adapter",
                        ["tenantSlug"] = tenantSlugForFileName,
                    }));
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
                    BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
                    await db.SaveChangesAsync(ct);
                    return;
                }

                run.Status = BackupRunStatus.Failed;
                run.CompletedAt = DateTime.UtcNow;
                run.FailureCode = result.ErrorCode ?? "EXECUTION_FAILED";
                run.FailureDetail = result.ErrorDetail;
                BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
                await db.SaveChangesAsync(ct);
                await BackupAutomaticRetryCoordinator.TrySchedulePendingRetryAfterTerminalSaveAsync(
                    db, run, opts, DateTime.UtcNow, _logger, ct);
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
                        ["adapterKind"] = adapter.AdapterKind,
                        ["tenantSlug"] = tenantSlugForFileName,
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
                BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
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
                BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
                await db.SaveChangesAsync(ct);
                await BackupAutomaticRetryCoordinator.TrySchedulePendingRetryAfterTerminalSaveAsync(
                    db, run, opts, DateTime.UtcNow, _logger, ct);
                _alerts.Publish(new BackupAlertEvent(
                    BackupAlertKind.VerificationFailed,
                    run.Id,
                    run.CorrelationId,
                    "Artifact metadata verifier threw.",
                    new Dictionary<string, string>
                    {
                        ["failureStage"] = "verifier_exception",
                        ["tenantSlug"] = tenantSlugForFileName,
                    }));
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
                    new Dictionary<string, string>
                    {
                        ["failureStage"] = "artifact_metadata_verification",
                        ["tenantSlug"] = tenantSlugForFileName,
                    }));
                BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
                await db.SaveChangesAsync(ct);
                await BackupAutomaticRetryCoordinator.TrySchedulePendingRetryAfterTerminalSaveAsync(
                    db, run, opts, DateTime.UtcNow, _logger, ct);
                return;
            }

            var completenessFailure = BackupCompletenessSuccessPolicy.GetIncompleteVerifiedArtifactSetFailureReason(
                opts.ExecutionAdapterKind,
                vOutcome);
            if (completenessFailure != null)
            {
                var gateDetails = string.IsNullOrWhiteSpace(vOutcome.DetailsJson)
                    ? new JsonObject()
                    : JsonNode.Parse(vOutcome.DetailsJson)!.AsObject();
                gateDetails["completenessGate"] = JsonSerializer.SerializeToNode(new
                {
                    failed = true,
                    requiredLogicalDumpInVerifiedSet = true,
                    executionAdapterKind = opts.ExecutionAdapterKind.ToString()
                });
                verification.Status = BackupVerificationStatus.Failed;
                verification.FailureReason = completenessFailure;
                verification.DetailsJson = gateDetails.ToJsonString();
                run.Status = BackupRunStatus.VerificationFailed;
                run.CompletedAt = DateTime.UtcNow;
                run.FailureCode = "INCOMPLETE_VERIFIED_ARTIFACT_SET";
                run.FailureDetail = completenessFailure;
                _logger.LogWarning(
                    "Backup completeness gate failed: runId={RunId}, adapterKind={AdapterKind}, reason={Reason}",
                    run.Id,
                    opts.ExecutionAdapterKind,
                    completenessFailure);
                _alerts.Publish(new BackupAlertEvent(
                    BackupAlertKind.VerificationFailed,
                    run.Id,
                    run.CorrelationId,
                    completenessFailure,
                    new Dictionary<string, string>
                    {
                        ["failureStage"] = "incomplete_verified_artifact_set",
                        ["tenantSlug"] = tenantSlugForFileName,
                    }));
                BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
                await db.SaveChangesAsync(ct);
                await BackupAutomaticRetryCoordinator.TrySchedulePendingRetryAfterTerminalSaveAsync(
                    db, run, opts, DateTime.UtcNow, _logger, ct);
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
                BackupAutomaticRetryCoordinator.ClearAutomaticRetryPlanningFieldsOnSuccess(run);
                await db.SaveChangesAsync(ct);
                try
                {
                    await postSuccess.NotifySucceededAsync(db, run, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Backup post-success hook failed after terminal success runId={RunId}",
                        run.Id);
                }
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
                BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
                await db.SaveChangesAsync(ct);
                await BackupAutomaticRetryCoordinator.TrySchedulePendingRetryAfterTerminalSaveAsync(
                    db, run, opts, DateTime.UtcNow, _logger, ct);
                _alerts.Publish(new BackupAlertEvent(
                    BackupAlertKind.VerificationFailed,
                    run.Id,
                    run.CorrelationId,
                    verification.FailureReason,
                    new Dictionary<string, string>
                    {
                        ["failureStage"] = "external_archive_precheck",
                        ["tenantSlug"] = tenantSlugForFileName,
                    }));
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
                BackupAutomaticRetryCoordinator.RecordTerminalFailureObservability(run);
                await db.SaveChangesAsync(ct);
                await BackupAutomaticRetryCoordinator.TrySchedulePendingRetryAfterTerminalSaveAsync(
                    db, run, opts, DateTime.UtcNow, _logger, ct);
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
                        ["errorCode"] = extOutcome.ErrorCode ?? "",
                        ["tenantSlug"] = tenantSlugForFileName,
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
            BackupAutomaticRetryCoordinator.ClearAutomaticRetryPlanningFieldsOnSuccess(run);
            await db.SaveChangesAsync(ct);
            try
            {
                await postSuccess.NotifySucceededAsync(db, run, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Backup post-success hook failed after terminal success runId={RunId}",
                    run.Id);
            }

            _logger.LogInformation(
                "Backup run succeeded after staging + external archive verification: runId={RunId}, adapterKind={AdapterKind}",
                run.Id,
                adapter.AdapterKind);
    }
}
