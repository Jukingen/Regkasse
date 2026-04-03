using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed partial class RestoreVerificationOrchestratorHostedService
{
    private async Task ExecuteRestoreVerificationRunWorkAsync(
        IServiceProvider sp,
        AppDbContext db,
        RestoreVerificationRun run,
        CancellationToken ct)
    {
        var backupOpts = _backupOpts.CurrentValue;
        var restoreOpts = _restoreOpts.CurrentValue;
        var evidenceBuilder = new RestoreDrillEvidenceBuilder();
        var details = new JsonObject
        {
            ["scope"] = "restore_confidence_drill_not_artifact_checksum",
            ["evidenceSchemaVersion"] = 5,
            ["tseRestoreVerification"] = "deferred_vendor_scope",
            ["finanzOnlineOutbox"] =
                "Restore drill does not replay FinanzOnline outbox; treat as separate operational concern.",
            ["integrityInterpretation"] =
                "When IncludeLiveIntegrityChecks runs against DefaultConnection, it validates current operational data only; post-restore continuity SQL runs against the ephemeral restored clone when configured.",
            ["validitySeparation"] = JsonSerializer.SerializeToNode(new
            {
                artifactAndRestoreTooling = "This drill validates dump readability and optional isolated restore plus post-restore SQL on the clone.",
                fiscalScriptOnConfiguredConnection =
                    "Fiscal go-live script runs on FiscalValidationConnectionStringName when set; not necessarily the restored ephemeral database.",
                liveOperationalIntegrity =
                    "IncludeLiveIntegrityChecks uses the live app database (DefaultConnection or configured), not the ephemeral restore.",
                applicationSmokeProbe =
                    "Optional HTTP GET to ApplicationSmokeProbeBaseUrl + relative path; proves reachability of a configured deployment, not necessarily the process running this drill.",
                restoredDatabaseApplicationSmoke =
                    "When RestoredDatabaseApplicationSmokeEnabled, in-process EF/migration + read-only checks against the ephemeral restored clone connection string before drop; L5 proof band uses ResultKind — only Passed advances strict L5a.",
                externalDependencies =
                    "ExternalDependencyRecovery evidence is configuration snapshot only (partial L6); does not prove FinanzOnline reachability, TSE hardware, or credential validity."
            })
        };

        RestoredDatabaseApplicationSmokeEvidenceBlock? restoredDbSmokeEvidence = null;
        var restoredDbApplicationSmokeBand = new RecoveryProofBand
        {
            Outcome = RecoveryProofOutcome.NotConfigured,
            Detail = "restored_database_application_smoke_not_in_scope"
        };

        var fallbackDepth = restoreOpts.DumpFallbackDepth;
        var backupRunQuery = sp.GetRequiredService<IBackupRunQueryService>();
        var candidateRunIds = restoreOpts.AllowNonPgDumpBackupSource
            ? await backupRunQuery.GetRecentSucceededRunIdsAsync(fallbackDepth, ct)
            : await backupRunQuery.GetRecentSucceededPgDumpRunIdsAsync(fallbackDepth, ct);

        details["candidateBackupSelection"] = JsonSerializer.SerializeToNode(new
        {
            allowNonPgDumpBackupSource = restoreOpts.AllowNonPgDumpBackupSource,
            candidateRunCount = candidateRunIds.Count,
            fallbackDepth
        });

        if (!restoreOpts.AllowNonPgDumpBackupSource && candidateRunIds.Count == 0)
        {
            await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                "NO_ELIGIBLE_BACKUP_RUN",
                $"No succeeded {nameof(BackupExecutionAdapterKind.PgDump)} backup runs in the last {fallbackDepth} attempts; widen DumpFallbackDepth or run a real pg_dump backup.",
                RestoreDrillFailureMapper.CategoryFromFailureCode("NO_ELIGIBLE_BACKUP_RUN"),
                RestoreDrillStage.None,
                BuildBands(artifactResolved: false), null, null, null, null, null, null, null, ct);
            return;
        }

        var dump = await RestoreVerificationDumpPathResolver.TryResolveAmongSucceededCandidatesAsync(
            db,
            backupOpts,
            candidateRunIds,
            _logger,
            _hostEnvironment,
            ct);

        if (dump == null)
        {
            details["dumpResolution"] = JsonSerializer.SerializeToNode(new
            {
                outcome = "no_dump_available",
                fallbackDepth,
                candidateRunCount = candidateRunIds.Count
            });
            await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                "NO_DUMP_AVAILABLE",
                $"No logical dump file found on disk for the {fallbackDepth} most recent successful backup run(s) (checked staging, then external archive per run).",
                RestoreDrillFailureMapper.CategoryFromFailureCode("NO_DUMP_AVAILABLE"),
                RestoreDrillStage.None,
                BuildBands(artifactResolved: false), null, null, null, null, null, null, null, ct);
            return;
        }

        run.SourceBackupRunId = dump.Value.backupRunId;
        run.SourceBackupArtifactId = dump.Value.artifactId;
        run.DumpRelativeDescriptor = dump.Value.relativeDescriptor;
        evidenceBuilder.Mark(RestoreDrillStage.ArtifactDiscovered, "source_artifact_resolved");
        run.RestoreDrillReachedStage = RestoreDrillStage.ArtifactDiscovered;

        var listResult = await _pgRestoreList.InspectDumpFileAsync(dump.Value.absolutePath, ct);
        run.PgRestoreListExitCode = listResult.ExitCode;
        run.PgRestoreListLineCount = listResult.NonEmptyLineCount;
        run.PgRestoreListPassed = listResult.Success;

        var inspectionPayload = new
        {
            passed = listResult.Success,
            exitCode = listResult.ExitCode,
            lineCount = listResult.NonEmptyLineCount,
            kind = "pg_restore_list_toc_inspection_not_checksum"
        };
        details["pgRestoreList"] = JsonSerializer.SerializeToNode(inspectionPayload);
        details["dumpInspection"] = JsonSerializer.SerializeToNode(inspectionPayload);

        if (!listResult.Success)
        {
            var sourceAdapter = await db.BackupRuns.AsNoTracking()
                .Where(r => r.Id == dump.Value.backupRunId)
                .Select(r => r.AdapterKind)
                .FirstOrDefaultAsync(ct);
            if (string.Equals(sourceAdapter, "Fake", StringComparison.OrdinalIgnoreCase))
            {
                details["pgRestoreListFailureContext"] = JsonSerializer.SerializeToNode(new
                {
                    sourceBackupRunId = dump.Value.backupRunId,
                    sourceAdapterKind = sourceAdapter,
                    reason = "fake_adapter_stub_not_pg_restore_format",
                });
            }

            await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                "PG_RESTORE_LIST_FAILED",
                listResult.StdErrSnippet ?? "pg_restore --list failed.",
                RestoreDrillFailureMapper.CategoryFromFailureCode("PG_RESTORE_LIST_FAILED"),
                RestoreDrillStage.ArtifactDiscovered,
                BuildBands(artifactResolved: true, pgList: false), null, null, null, null, null, null, null, ct);
            return;
        }

        evidenceBuilder.Mark(RestoreDrillStage.PgRestoreListPassed, "toc_readable");
        run.RestoreDrillReachedStage = RestoreDrillStage.PgRestoreListPassed;

        var restoreAttemptNode = new JsonObject();
        details["restoreAttempt"] = restoreAttemptNode;
        var isoEnabled = restoreOpts.IsolatedPgRestoreEnabled;
        var isoConnName = restoreOpts.IsolatedRestoreAdminConnectionStringName;
        PostRestoreSqlEvidenceBlock? postRestoreBlock = null;

        if (!isoEnabled)
        {
            run.RestoreAttemptExecuted = false;
            run.RestoreAttemptPassed = null;
            run.RestoreAttemptExitCode = null;
            run.RestoreAttemptSkipReason = "ISOLATED_PG_RESTORE_DISABLED";
            run.RestoreTargetDbRedacted = null;
            restoreAttemptNode["skipped"] = true;
            restoreAttemptNode["reason"] = run.RestoreAttemptSkipReason;
            details["postRestoreContinuitySql"] = JsonSerializer.SerializeToNode(new
            {
                skipped = true,
                reason = "isolated_pg_restore_disabled_no_ephemeral_database"
            });
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
            await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                "RESTORE_ATTEMPT_NOT_ALLOWED",
                "Isolated restore admin connection must not be DefaultConnection in Production.",
                RestoreDrillFailureMapper.CategoryFromFailureCode("RESTORE_ATTEMPT_NOT_ALLOWED"),
                RestoreDrillStage.PgRestoreListPassed,
                BuildBands(artifactResolved: true, pgList: true, isolated: false), null, null, null, null, null, null, null, ct);
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
                await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                    "ISOLATED_RESTORE_NOT_CONFIGURED",
                    "IsolatedPgRestoreEnabled is true but IsolatedRestoreAdminConnectionStringName is missing or empty in configuration.",
                    RestoreDrillFailureMapper.CategoryFromFailureCode("ISOLATED_RESTORE_NOT_CONFIGURED"),
                    RestoreDrillStage.PgRestoreListPassed,
                    BuildBands(artifactResolved: true, pgList: true, isolated: false), null, null, null, null, null, null, null, ct);
                return;
            }

            var dbName = $"rv_v_{run.Id:N}";
            run.RestoreTargetDbRedacted = dbName;
            var timeoutSec = restoreOpts.IsolatedPgRestoreTimeoutSeconds <= 0
                ? 3600
                : restoreOpts.IsolatedPgRestoreTimeoutSeconds;
            var timeout = TimeSpan.FromSeconds(Math.Max(60, timeoutSec));

            var keepEphemeralForDrillChecks = restoreOpts.PostRestoreSqlChecksEnabled
                                              || restoreOpts.RestoredDatabaseApplicationSmokeEnabled;
            var dropAfterRestore = !keepEphemeralForDrillChecks;

            _logger.LogInformation(
                "Restore verification isolated pg_restore: runId={RunId}, backupRunId={BackupRunId}, targetDb={TargetDb}, dropAfterRestore={Drop}",
                run.Id,
                run.SourceBackupRunId,
                dbName,
                dropAfterRestore);

            PgRestoreIsolatedRestoreOutcome isolatedOutcome;
            try
            {
                isolatedOutcome = await _isolatedRestore.RestoreCustomDumpToEphemeralDatabaseAsync(
                    adminCs,
                    dump.Value.absolutePath,
                    dbName,
                    restoreOpts.PgRestoreExecutablePath,
                    timeout,
                    dropAfterRestore,
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
                await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                    "ISOLATED_PG_RESTORE_CANCELLED",
                    "Restore verification cancelled during isolated pg_restore.",
                    RestoreDrillFailureMapper.CategoryFromFailureCode("ISOLATED_PG_RESTORE_CANCELLED"),
                    RestoreDrillStage.PgRestoreListPassed,
                    BuildBands(artifactResolved: true, pgList: true, isolated: false), null, null, null, null, null, null, null, ct);
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
                await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                    "ISOLATED_PG_RESTORE_EXCEPTION",
                    ex.Message,
                    RestoreDrillFailureMapper.CategoryFromFailureCode("ISOLATED_PG_RESTORE_EXCEPTION"),
                    RestoreDrillStage.PgRestoreListPassed,
                    BuildBands(artifactResolved: true, pgList: true, isolated: false), null, null, null, null, null, null, null, ct);
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
                await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                    "ISOLATED_PG_RESTORE_FAILED",
                    isolatedOutcome.StdErrSnippet ?? "pg_restore into ephemeral database failed.",
                    RestoreDrillFailureMapper.CategoryFromFailureCode("ISOLATED_PG_RESTORE_FAILED"),
                    RestoreDrillStage.PgRestoreListPassed,
                    BuildBands(artifactResolved: true, pgList: true, isolated: false), null, null, null, null, null, null, null, ct);
                return;
            }

            evidenceBuilder.Mark(RestoreDrillStage.RestoreAttemptPassed, "pg_restore_exit_0");
            run.RestoreDrillReachedStage = RestoreDrillStage.RestoreAttemptPassed;

            if (restoreOpts.PostRestoreSqlChecksEnabled)
            {
                try
                {
                    var targetCs = PostRestoreDrillSqlChecker.BuildTargetDatabaseConnectionString(adminCs, dbName);
                    var continuity = await _postRestoreSqlChecker.RunContinuityChecksAsync(targetCs, ct);
                    run.PostRestoreContinuityChecksExecuted = true;
                    run.PostRestoreContinuityChecksPassed = continuity.Passed && continuity.Executed;
                    var continuityProof = PostRestoreContinuityEvidenceFormatter.MapProofState(continuity);
                    postRestoreBlock = new PostRestoreSqlEvidenceBlock
                    {
                        ProofLayerId = "L4_post_restore_continuity",
                        ContinuityChecksResult = continuityProof,
                        ContinuityChecksSummary = PostRestoreContinuityEvidenceFormatter.BuildContinuityChecksSummary(continuity),
                        StartedAtUtc = continuity.StartedAtUtc,
                        CompletedAtUtc = continuity.CompletedAtUtc,
                        DurationMs = continuity.DurationMs,
                        Executed = continuity.Executed,
                        Passed = continuity.Passed,
                        SkipReason = continuity.Executed ? null : continuity.ErrorDetail,
                        DominantFailureCategory = continuity.Executed && continuity.Passed
                            ? null
                            : PostRestoreSqlFailureCategoryMapper.ComputeDominantFailureCategory(continuity.Checks),
                        Checks = continuity.Checks.ToList()
                    };
                    run.PostRestoreL4ContinuityProofState = continuityProof;
                    details["postRestoreContinuitySql"] = JsonSerializer.SerializeToNode(new
                    {
                        executed = continuity.Executed,
                        passed = continuity.Passed,
                        checkCount = continuity.Checks.Count
                    });

                    if (!continuity.Executed || !continuity.Passed)
                    {
                        await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                            "POST_RESTORE_CONTINUITY_SQL_FAILED",
                            continuity.ErrorDetail ?? "Post-restore continuity SQL checks failed.",
                            RestoreDrillFailureCategory.PostRestoreContinuitySql,
                            RestoreDrillStage.RestoreAttemptPassed,
                            BuildBands(artifactResolved: true, pgList: true, isolated: true, postSql: false),
                            postRestoreBlock,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            ct);
                        return;
                    }

                    evidenceBuilder.Mark(RestoreDrillStage.PostRestoreContinuitySqlPassed, "continuity_sql_ok");
                    run.RestoreDrillReachedStage = RestoreDrillStage.PostRestoreContinuitySqlPassed;

                    if (restoreOpts.RestoredDatabaseApplicationSmokeEnabled)
                    {
                        var smokeOutcome = await _restoredDatabaseApplicationSmoke.RunAsync(targetCs, ct);
                        restoredDbSmokeEvidence = new RestoredDatabaseApplicationSmokeEvidenceBlock
                        {
                            Executed = true,
                            ResultKind = smokeOutcome.Kind,
                            ApplicationSmokeResult =
                                RestoredDatabaseApplicationSmokeEvidenceFormatter.MapApplicationSmokeResult(smokeOutcome.Kind),
                            ApplicationSmokeSummary = RestoredDatabaseApplicationSmokeEvidenceFormatter
                                .BuildApplicationSmokeSummary(smokeOutcome.Kind, smokeOutcome.Detail, smokeOutcome.DurationMs),
                            Detail = smokeOutcome.Detail,
                            DurationMs = smokeOutcome.DurationMs,
                            StartedAtUtc = smokeOutcome.StartedAtUtc,
                            CompletedAtUtc = smokeOutcome.CompletedAtUtc,
                            Checks = smokeOutcome.Checks.ToList()
                        };
                        restoredDbApplicationSmokeBand =
                            RestoreDrillApplicationSmokeEvidenceMapper.ToValidityBand(smokeOutcome.Kind, smokeOutcome.Detail);
                        details["restoredDatabaseApplicationSmoke"] = JsonSerializer.SerializeToNode(new
                        {
                            executed = true,
                            resultKind = smokeOutcome.Kind.ToString(),
                            durationMs = smokeOutcome.DurationMs,
                            checkCount = smokeOutcome.Checks.Count
                        });

                        if (smokeOutcome.Kind == RestoreDrillApplicationSmokeResultKind.Failed)
                        {
                            await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                                "RESTORED_DB_APPLICATION_SMOKE_FAILED",
                                smokeOutcome.Detail ?? "Restored database application smoke failed.",
                                RestoreDrillFailureCategory.RestoredDatabaseApplicationSmoke,
                                RestoreDrillStage.PostRestoreContinuitySqlPassed,
                                BuildBands(artifactResolved: true, pgList: true, isolated: true, postSql: true,
                                    restoredDatabaseApplicationSmoke: restoredDbApplicationSmokeBand),
                                postRestoreBlock,
                                null,
                                null,
                                null,
                                null,
                                restoredDbSmokeEvidence,
                                restoredDbApplicationSmokeBand,
                                ct);
                            return;
                        }

                        if (smokeOutcome.Kind == RestoreDrillApplicationSmokeResultKind.Passed)
                        {
                            evidenceBuilder.Mark(RestoreDrillStage.RestoredDatabaseApplicationSmokePassed,
                                "restored_db_application_smoke_ok");
                            run.RestoreDrillReachedStage = RestoreDrillStage.RestoredDatabaseApplicationSmokePassed;
                        }
                    }
                    else
                    {
                        restoredDbSmokeEvidence = new RestoredDatabaseApplicationSmokeEvidenceBlock
                        {
                            Executed = false,
                            ResultKind = RestoreDrillApplicationSmokeResultKind.NotAttempted,
                            ApplicationSmokeResult = RestoredDatabaseApplicationSmokeEvidenceFormatter.MapApplicationSmokeResult(
                                RestoreDrillApplicationSmokeResultKind.NotAttempted),
                            ApplicationSmokeSummary = RestoredDatabaseApplicationSmokeEvidenceFormatter.BuildApplicationSmokeSummary(
                                RestoreDrillApplicationSmokeResultKind.NotAttempted,
                                "RestoredDatabaseApplicationSmokeEnabled=false",
                                null),
                            Detail = "RestoredDatabaseApplicationSmokeEnabled=false"
                        };
                        restoredDbApplicationSmokeBand = RestoreDrillApplicationSmokeEvidenceMapper.ToValidityBand(
                            RestoreDrillApplicationSmokeResultKind.NotAttempted,
                            "feature_disabled");
                    }
                }
                finally
                {
                    await _isolatedRestore.DropEphemeralDatabaseAsync(adminCs, dbName, ct);
                    restoreAttemptNode["ephemeralDbDropped"] = true;
                }
            }
            else
            {
                restoreAttemptNode["ephemeralDbDropped"] = dropAfterRestore;
                details["postRestoreContinuitySql"] = JsonSerializer.SerializeToNode(new
                {
                    skipped = true,
                    reason = "PostRestoreSqlChecksEnabled=false"
                });
            }

            if (!restoreOpts.PostRestoreSqlChecksEnabled)
            {
                restoreAttemptNode["fiscalNote"] =
                    "Fiscal SQL does not automatically run against the ephemeral DB; use FiscalValidationConnectionStringName for a clone if post-restore fiscal checks are required.";
            }
        }

        FiscalSqlEvidenceBlock? fiscalBlock = null;
        var fiscalName = restoreOpts.FiscalValidationConnectionStringName;
        if (string.IsNullOrWhiteSpace(fiscalName))
        {
            run.FiscalSqlSkipped = true;
            run.FiscalSqlSkipReason = "FISCAL_CONNECTION_NOT_CONFIGURED";
            details["fiscalSql"] = JsonSerializer.SerializeToNode(new { skipped = true, reason = run.FiscalSqlSkipReason });
            fiscalBlock = new FiscalSqlEvidenceBlock
            {
                Executed = false,
                SkipReason = run.FiscalSqlSkipReason,
                ValidityScopeNote = "fiscal_script_not_run"
            };
        }
        else if (_hostEnvironment.IsProduction()
                 && string.Equals(fiscalName.Trim(), "DefaultConnection", StringComparison.OrdinalIgnoreCase))
        {
            run.FiscalSqlSkipped = true;
            run.FiscalSqlSkipReason = "PRODUCTION_REQUIRES_NON_DEFAULT_CONNECTION";
            details["fiscalSql"] = JsonSerializer.SerializeToNode(new { skipped = true, reason = run.FiscalSqlSkipReason });
            fiscalBlock = new FiscalSqlEvidenceBlock
            {
                Executed = false,
                SkipReason = run.FiscalSqlSkipReason,
                ValidityScopeNote = "production_blocks_default_connection"
            };
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
                await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                    "FISCAL_VALIDATION_NOT_EXECUTED",
                    "FiscalValidationConnectionStringName is set but the connection string entry is missing.",
                    RestoreDrillFailureMapper.CategoryFromFailureCode("FISCAL_VALIDATION_NOT_EXECUTED"),
                    run.RestoreDrillReachedStage,
                    BuildBands(artifactResolved: true, pgList: true, isolated: run.RestoreAttemptExecuted && run.RestoreAttemptPassed == true,
                        postSql: run.PostRestoreContinuityChecksPassed,
                        restoredDatabaseApplicationSmoke: restoredDbApplicationSmokeBand),
                    postRestoreBlock,
                    fiscalBlock,
                    null,
                    null,
                    null,
                    null,
                    null,
                    ct);
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
                fiscalBlock = new FiscalSqlEvidenceBlock
                {
                    Executed = false,
                    SkipReason = run.FiscalSqlSkipReason,
                    ValidityScopeNote = "configured_connection:" + fiscalName.Trim()
                };
                await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                    "FISCAL_VALIDATION_NOT_EXECUTED",
                    fiscalOutcome.ErrorDetail ?? "Fiscal validation could not run.",
                    RestoreDrillFailureMapper.CategoryFromFailureCode("FISCAL_VALIDATION_NOT_EXECUTED"),
                    run.RestoreDrillReachedStage,
                    BuildBands(artifactResolved: true, pgList: true, isolated: run.RestoreAttemptExecuted && run.RestoreAttemptPassed == true,
                        postSql: run.PostRestoreContinuityChecksPassed,
                        restoredDatabaseApplicationSmoke: restoredDbApplicationSmokeBand),
                    postRestoreBlock,
                    fiscalBlock,
                    null,
                    null,
                    null,
                    null,
                    null,
                    ct);
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

            fiscalBlock = new FiscalSqlEvidenceBlock
            {
                Executed = true,
                Passed = fiscalOutcome.Passed,
                ValidityScopeNote =
                    "configured_connection_not_necessarily_restored_ephemeral:" + fiscalName.Trim()
            };

            if (!fiscalOutcome.Passed)
            {
                await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                    "FISCAL_VALIDATION_FAILED",
                    fiscalOutcome.ErrorDetail ?? fiscalOutcome.SummaryLine ?? "Fiscal SQL reported FAIL rows.",
                    RestoreDrillFailureMapper.CategoryFromFailureCode("FISCAL_VALIDATION_FAILED"),
                    run.RestoreDrillReachedStage,
                    BuildBands(artifactResolved: true, pgList: true, isolated: run.RestoreAttemptExecuted && run.RestoreAttemptPassed == true,
                        postSql: run.PostRestoreContinuityChecksPassed, fiscal: false,
                        restoredDatabaseApplicationSmoke: restoredDbApplicationSmokeBand),
                    postRestoreBlock,
                    fiscalBlock,
                    null,
                    null,
                    null,
                    null,
                    null,
                    ct);
                return;
            }

            evidenceBuilder.Mark(RestoreDrillStage.FiscalSqlScriptPassed, "fiscal_go_live_script");
            run.RestoreDrillReachedStage = RestoreDrillStage.FiscalSqlScriptPassed;
        }

        LiveIntegrityEvidenceBlock? liveBlock = null;
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

            liveBlock = new LiveIntegrityEvidenceBlock
            {
                Executed = true,
                Passed = pass,
                Scope = run.IntegrityScope
            };

            if (!pass)
            {
                await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                    "INTEGRITY_CHECKS_FAILED",
                    "Operational integrity report reported issues in lookback window.",
                    RestoreDrillFailureMapper.CategoryFromFailureCode("INTEGRITY_CHECKS_FAILED"),
                    run.RestoreDrillReachedStage,
                    BuildBands(artifactResolved: true, pgList: true, isolated: run.RestoreAttemptExecuted && run.RestoreAttemptPassed == true,
                        postSql: run.PostRestoreContinuityChecksPassed, fiscal: run.FiscalSqlPassed == true, liveIntegrity: false,
                        restoredDatabaseApplicationSmoke: restoredDbApplicationSmokeBand),
                    postRestoreBlock,
                    fiscalBlock,
                    liveBlock,
                    null,
                    null,
                    null,
                    null,
                    ct);
                return;
            }

            evidenceBuilder.Mark(RestoreDrillStage.LiveOperationalIntegrityPassed, "live_integrity_ok");
            run.RestoreDrillReachedStage = RestoreDrillStage.LiveOperationalIntegrityPassed;
        }
        else
        {
            run.IntegrityChecksPassed = null;
            details["integrity"] = JsonSerializer.SerializeToNode(new { skipped = true });
            liveBlock = new LiveIntegrityEvidenceBlock { Executed = false, Passed = null, Scope = "skipped_by_configuration" };
        }

        var externalBlock = _externalDependencyEvidence.Build();
        details["externalDependencyRecovery"] = JsonSerializer.SerializeToNode(new
        {
            overallOutcome = externalBlock.OverallOutcome.ToString(),
            l6EvidenceSchemaVersion = externalBlock.L6EvidenceSchemaVersion,
            l6OverallState = externalBlock.Rollup?.OverallState.ToString(),
            interpretation = externalBlock.Interpretation,
            domainCount = externalBlock.Domains.Count,
            checkCount = externalBlock.Checks.Count
        });

        ApplicationSmokeProbeEvidenceBlock? appSmokeEvidence = null;
        var appRecoveryBand = new RecoveryProofBand
        {
            Outcome = RecoveryProofOutcome.NotConfigured,
            Detail = "application_smoke_probe_disabled"
        };

        if (restoreOpts.ApplicationSmokeProbeEnabled)
        {
            if (!Uri.TryCreate(restoreOpts.ApplicationSmokeProbeBaseUrl!.Trim(), UriKind.Absolute, out var baseUri)
                || (!string.Equals(baseUri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(baseUri.Scheme, "https", StringComparison.OrdinalIgnoreCase)))
            {
                await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                    "APPLICATION_SMOKE_INVALID_BASE_URL",
                    "ApplicationSmokeProbeBaseUrl must be an absolute http or https URL.",
                    RestoreDrillFailureMapper.CategoryFromFailureCode("APPLICATION_SMOKE_INVALID_BASE_URL"),
                    run.RestoreDrillReachedStage,
                    BuildBands(
                        artifactResolved: true,
                        pgList: true,
                        isolated: run.RestoreAttemptExecuted ? run.RestoreAttemptPassed : null,
                        postSql: run.PostRestoreContinuityChecksExecuted ? run.PostRestoreContinuityChecksPassed : null,
                        fiscal: run.FiscalSqlSkipped ? null : run.FiscalSqlPassed,
                        liveIntegrity: restoreOpts.IncludeLiveIntegrityChecks ? run.IntegrityChecksPassed : null,
                        restoredDatabaseApplicationSmoke: restoredDbApplicationSmokeBand),
                    postRestoreBlock,
                    fiscalBlock,
                    liveBlock,
                    null,
                    null,
                    null,
                    null,
                    ct);
                return;
            }

            var timeoutSec = restoreOpts.ApplicationSmokeProbeTimeoutSeconds <= 0
                ? 30
                : restoreOpts.ApplicationSmokeProbeTimeoutSeconds;
            var probeOutcome = await _applicationSmokeProbe.ProbeAsync(
                baseUri,
                restoreOpts.ApplicationSmokeProbeRelativePath,
                TimeSpan.FromSeconds(Math.Max(5, timeoutSec)),
                ct);

            appSmokeEvidence = new ApplicationSmokeProbeEvidenceBlock
            {
                Executed = true,
                Passed = probeOutcome.Success,
                ApplicationSmokeResult = ApplicationSmokeProbeEvidenceFormatter.MapApplicationSmokeResult(probeOutcome.Success),
                ApplicationSmokeSummary = ApplicationSmokeProbeEvidenceFormatter.BuildApplicationSmokeSummary(
                    probeOutcome.Success,
                    probeOutcome.HttpStatusCode,
                    probeOutcome.DurationMs,
                    probeOutcome.RequestPath),
                StartedAtUtc = probeOutcome.StartedAtUtc,
                CompletedAtUtc = probeOutcome.CompletedAtUtc,
                HttpStatusCode = probeOutcome.HttpStatusCode,
                DurationMs = probeOutcome.DurationMs,
                RequestPath = probeOutcome.RequestPath,
                Error = probeOutcome.Error
            };

            appRecoveryBand = new RecoveryProofBand
            {
                Outcome = probeOutcome.Success ? RecoveryProofOutcome.Passed : RecoveryProofOutcome.Failed,
                Detail = probeOutcome.Success ? "http_2xx" : (probeOutcome.Error ?? "http_smoke_failed")
            };

            details["applicationSmokeProbe"] = JsonSerializer.SerializeToNode(new
            {
                executed = true,
                passed = probeOutcome.Success,
                httpStatus = probeOutcome.HttpStatusCode,
                durationMs = probeOutcome.DurationMs
            });

            if (!probeOutcome.Success)
            {
                await FailRunAsync(db, run, evidenceBuilder, details, RestoreVerificationStatus.Failed,
                    "APPLICATION_SMOKE_PROBE_FAILED",
                    probeOutcome.Error ?? $"HTTP smoke probe failed (status={probeOutcome.HttpStatusCode?.ToString() ?? "n/a"}).",
                    RestoreDrillFailureCategory.ApplicationSmokeProbe,
                    run.RestoreDrillReachedStage,
                    BuildBands(
                        artifactResolved: true,
                        pgList: true,
                        isolated: run.RestoreAttemptExecuted ? run.RestoreAttemptPassed : null,
                        postSql: run.PostRestoreContinuityChecksExecuted ? run.PostRestoreContinuityChecksPassed : null,
                        fiscal: run.FiscalSqlSkipped ? null : run.FiscalSqlPassed,
                        liveIntegrity: restoreOpts.IncludeLiveIntegrityChecks ? run.IntegrityChecksPassed : null,
                        fiscalContinuityLayer: false,
                        restoredDatabaseApplicationSmoke: restoredDbApplicationSmokeBand,
                        applicationRecovery: appRecoveryBand,
                        externalDependencyRecovery: ExternalDependencyProofBandMapper.ToRecoveryProofBand(externalBlock)),
                    postRestoreBlock,
                    fiscalBlock,
                    liveBlock,
                    appSmokeEvidence,
                    appRecoveryBand,
                    null,
                    null,
                    ct);
                return;
            }

            evidenceBuilder.Mark(RestoreDrillStage.ApplicationSmokePassed, "http_smoke_ok");
            run.RestoreDrillReachedStage = RestoreDrillStage.ApplicationSmokePassed;
        }
        else
        {
            details["applicationSmokeProbe"] = JsonSerializer.SerializeToNode(new { skipped = true, reason = "disabled" });
        }

        var extRecoveryBand = ExternalDependencyProofBandMapper.ToRecoveryProofBand(externalBlock);

        var fiscalContinuityPassed = RestoreDrillFiscalContinuityLayer.ComputeEvidence(restoreOpts, run);
        run.FiscalContinuityLayerPassed = fiscalContinuityPassed;
        run.ApplicationSmokeProbeExecuted = appSmokeEvidence?.Executed ?? false;
        run.ApplicationSmokeProbePassed = appSmokeEvidence?.Passed;
        StampExternalDependencyL6Columns(run, externalBlock);

        if (restoredDbSmokeEvidence != null)
        {
            run.RestoredDatabaseApplicationSmokeExecuted = restoredDbSmokeEvidence.Executed;
            run.RestoredDatabaseApplicationSmokeResultKind = restoredDbSmokeEvidence.ResultKind.ToString();
            run.RestoredDatabaseApplicationSmokePassed = restoredDbSmokeEvidence.ResultKind switch
            {
                RestoreDrillApplicationSmokeResultKind.Passed => true,
                RestoreDrillApplicationSmokeResultKind.Failed => false,
                _ => null
            };
        }

        run.Status = RestoreVerificationStatus.Succeeded;
        run.CompletedAt = DateTime.UtcNow;
        run.FailureCode = null;
        run.FailureDetail = null;
        run.FailureCategory = RestoreDrillFailureCategory.None;
        run.RestoreDrillReachedStage = RestoreDrillStage.Completed;
        StampDuration(run);
        run.DetailsJson = details.ToJsonString();
        run.EvidenceJson = RestoreDrillEvidenceJson.Serialize(new RestoreDrillEvidenceDocument
        {
            SchemaVersion = 5,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            RestoreVerificationRunId = run.Id,
            SourceBackupRunId = run.SourceBackupRunId,
            SourceBackupArtifactId = run.SourceBackupArtifactId,
            Validity = BuildBands(
                artifactResolved: true,
                pgList: true,
                isolated: run.RestoreAttemptExecuted ? run.RestoreAttemptPassed : null,
                postSql: run.PostRestoreContinuityChecksExecuted ? run.PostRestoreContinuityChecksPassed : null,
                fiscal: run.FiscalSqlSkipped ? null : run.FiscalSqlPassed,
                liveIntegrity: restoreOpts.IncludeLiveIntegrityChecks ? run.IntegrityChecksPassed : null,
                fiscalContinuityLayer: fiscalContinuityPassed,
                restoredDatabaseApplicationSmoke: restoredDbApplicationSmokeBand,
                applicationRecovery: appRecoveryBand,
                externalDependencyRecovery: extRecoveryBand),
            Stages = evidenceBuilder.Stages,
            PostRestoreContinuity = postRestoreBlock,
            FiscalSql = fiscalBlock,
            LiveOperationalIntegrity = liveBlock,
            ApplicationSmokeProbe = appSmokeEvidence,
            RestoredDatabaseApplicationSmoke = restoredDbSmokeEvidence,
            ExternalDependencyRecovery = externalBlock
        });
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Restore verification drill succeeded: runId={RunId}, backupRunId={BackupRunId}",
            run.Id,
            run.SourceBackupRunId);
    }

    private static RestoreDrillValidityBands BuildBands(
        bool artifactResolved,
        bool? pgList = null,
        bool? isolated = null,
        bool? postSql = null,
        bool? fiscal = null,
        bool? liveIntegrity = null,
        bool? fiscalContinuityLayer = null,
        RecoveryProofBand? restoredDatabaseApplicationSmoke = null,
        RecoveryProofBand? applicationRecovery = null,
        RecoveryProofBand? externalDependencyRecovery = null) =>
        new()
        {
            ArtifactResolved = artifactResolved,
            PgRestoreListArtifactReadable = pgList,
            IsolatedRestoreMaterialized = isolated,
            PostRestoreBusinessContinuitySqlPassed = postSql,
            FiscalGoLiveScriptOnConfiguredConnectionPassed = fiscal,
            LiveOperationalIntegrityChecksPassed = liveIntegrity,
            FiscalContinuityLayerPassed = fiscalContinuityLayer,
            RestoredDatabaseApplicationSmoke = restoredDatabaseApplicationSmoke ?? new RecoveryProofBand
            {
                Outcome = RecoveryProofOutcome.NotConfigured,
                Detail = "restored_database_application_smoke_not_attempted"
            },
            ApplicationRecovery = applicationRecovery ?? new RecoveryProofBand
            {
                Outcome = RecoveryProofOutcome.NotConfigured,
                Detail = "application_smoke_probe_not_configured"
            },
            ExternalDependencyRecovery = externalDependencyRecovery ?? new RecoveryProofBand
            {
                Outcome = RecoveryProofOutcome.Partial,
                Detail = "configuration_snapshot_only"
            }
        };

    private static void StampDuration(RestoreVerificationRun run)
    {
        if (run.StartedAt.HasValue && run.CompletedAt.HasValue)
            run.DurationMs = (long)(run.CompletedAt.Value - run.StartedAt.Value).TotalMilliseconds;
    }

    private static void StampExternalDependencyL6Columns(RestoreVerificationRun run, ExternalDependencyRecoveryEvidenceBlock block)
    {
        run.ExternalDependencyProofOutcome = block.OverallOutcome.ToString();
        if (block.Rollup != null)
        {
            run.ExternalDependencyL6OverallState = block.Rollup.OverallState.ToString();
            run.ExternalDependencyL6Summary = block.Rollup.Summary;
        }
        else
        {
            run.ExternalDependencyL6OverallState = null;
            run.ExternalDependencyL6Summary = null;
        }
    }

    private async Task FailRunAsync(
        AppDbContext db,
        RestoreVerificationRun run,
        RestoreDrillEvidenceBuilder evidenceBuilder,
        JsonObject details,
        RestoreVerificationStatus status,
        string failureCode,
        string failureDetail,
        RestoreDrillFailureCategory category,
        RestoreDrillStage? reachedStage,
        RestoreDrillValidityBands bands,
        PostRestoreSqlEvidenceBlock? postRestore,
        FiscalSqlEvidenceBlock? fiscal,
        LiveIntegrityEvidenceBlock? live,
        ApplicationSmokeProbeEvidenceBlock? applicationSmoke,
        RecoveryProofBand? applicationRecoveryOverride,
        RestoredDatabaseApplicationSmokeEvidenceBlock? restoredDbSmoke,
        RecoveryProofBand? restoredDbApplicationSmokeOverride,
        CancellationToken ct)
    {
        run.Status = status;
        run.CompletedAt = DateTime.UtcNow;
        run.FailureCode = failureCode;
        run.FailureDetail = failureDetail;
        run.FailureCategory = category;
        if (reachedStage.HasValue && reachedStage.Value != RestoreDrillStage.None)
            run.RestoreDrillReachedStage = reachedStage;
        run.FiscalContinuityLayerPassed = null;
        run.ApplicationSmokeProbeExecuted = applicationSmoke?.Executed ?? false;
        run.ApplicationSmokeProbePassed = applicationSmoke?.Passed;

        if (postRestore != null)
            run.PostRestoreL4ContinuityProofState = postRestore.ContinuityChecksResult;

        if (restoredDbSmoke != null)
        {
            run.RestoredDatabaseApplicationSmokeExecuted = restoredDbSmoke.Executed;
            run.RestoredDatabaseApplicationSmokeResultKind = restoredDbSmoke.ResultKind.ToString();
            run.RestoredDatabaseApplicationSmokePassed = restoredDbSmoke.ResultKind switch
            {
                RestoreDrillApplicationSmokeResultKind.Passed => true,
                RestoreDrillApplicationSmokeResultKind.Failed => false,
                _ => null
            };
        }

        var extBlock = _externalDependencyEvidence.Build();
        StampExternalDependencyL6Columns(run, extBlock);

        var mergedValidity = new RestoreDrillValidityBands
        {
            ArtifactResolved = bands.ArtifactResolved,
            PgRestoreListArtifactReadable = bands.PgRestoreListArtifactReadable,
            IsolatedRestoreMaterialized = bands.IsolatedRestoreMaterialized,
            PostRestoreBusinessContinuitySqlPassed = bands.PostRestoreBusinessContinuitySqlPassed,
            FiscalGoLiveScriptOnConfiguredConnectionPassed = bands.FiscalGoLiveScriptOnConfiguredConnectionPassed,
            LiveOperationalIntegrityChecksPassed = bands.LiveOperationalIntegrityChecksPassed,
            FiscalContinuityLayerPassed = bands.FiscalContinuityLayerPassed,
            RestoredDatabaseApplicationSmoke = restoredDbApplicationSmokeOverride ?? bands.RestoredDatabaseApplicationSmoke,
            ApplicationRecovery = applicationRecoveryOverride ?? bands.ApplicationRecovery,
            ExternalDependencyRecovery = ExternalDependencyProofBandMapper.ToRecoveryProofBand(extBlock)
        };

        StampDuration(run);
        run.DetailsJson = details.ToJsonString();
        run.EvidenceJson = RestoreDrillEvidenceJson.Serialize(new RestoreDrillEvidenceDocument
        {
            SchemaVersion = 5,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            RestoreVerificationRunId = run.Id,
            SourceBackupRunId = run.SourceBackupRunId,
            SourceBackupArtifactId = run.SourceBackupArtifactId,
            Validity = mergedValidity,
            Stages = evidenceBuilder.Stages,
            PostRestoreContinuity = postRestore,
            FiscalSql = fiscal,
            LiveOperationalIntegrity = live,
            ApplicationSmokeProbe = applicationSmoke,
            RestoredDatabaseApplicationSmoke = restoredDbSmoke,
            ExternalDependencyRecovery = extBlock
        });
        await db.SaveChangesAsync(ct);
    }
}
