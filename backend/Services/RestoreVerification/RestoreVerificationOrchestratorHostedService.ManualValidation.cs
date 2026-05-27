using System.Text.Json;
using System.Text.Json.Nodes;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed partial class RestoreVerificationOrchestratorHostedService
{
  private async Task<bool> TryExecuteManualValidationRestoreAsync(
      IServiceProvider sp,
      AppDbContext db,
      RestoreVerificationRun run,
      string adminConnectionString,
      (Guid backupRunId, Guid artifactId, string absolutePath, string relativeDescriptor) dump,
      RestoreDrillEvidenceBuilder evidenceBuilder,
      JsonObject details,
      JsonObject restoreAttemptNode,
      CancellationToken cancellationToken)
  {
      if (!ManualRestoreRunDetailsJson.IsValidationOnlyManualRestore(run.DetailsJson))
          return false;

      var targetDb = ManualRestoreRunDetailsJson.TryGetTargetDatabaseName(run.DetailsJson);
      if (string.IsNullOrEmpty(targetDb) || run.SourceBackupRunId is not Guid backupRunId)
          return false;

      var validation = sp.GetRequiredService<IValidationRestoreExecutionService>();
      var result = await validation.ExecuteValidationRestoreAsync(
          new ValidationRestoreExecutionRequest
          {
              BackupRunId = backupRunId,
              TargetDatabaseName = targetDb,
              ValidationOnly = true
          },
          cancellationToken);

      run.RestoreTargetDbRedacted = targetDb;
      run.RestoreAttemptExecuted = true;
      run.RestoreAttemptPassed = result.Success;
      restoreAttemptNode["validationOnlyExecution"] = true;
      restoreAttemptNode["executed"] = true;
      restoreAttemptNode["passed"] = result.Success;
      restoreAttemptNode["targetDbRedacted"] = targetDb;

      details["validationRestoreResult"] = JsonSerializer.SerializeToNode(new
      {
          success = result.Success,
          error = result.Error,
          summary = result.Summary
      });

      if (result.Success && result.Summary != null)
      {
          run.PostRestoreContinuityChecksExecuted = true;
          run.PostRestoreContinuityChecksPassed = result.Summary.IntegrityChecksPassed;
          run.FiscalSqlSkipped = result.Summary.FiscalValidationPassed is null;
          run.FiscalSqlPassed = result.Summary.FiscalValidationPassed;
          run.FiscalSqlFailCount = result.Summary.FiscalValidationFailCount;
          run.FiscalSqlWarnCount = result.Summary.FiscalValidationWarnCount;
      }

      var manualRequestId = ManualRestoreRunDetailsJson.TryGetManualRestoreRequestId(run.DetailsJson);
      ManualRestoreRequest? manualEntity = null;
      if (manualRequestId is Guid reqId)
      {
          manualEntity = await db.ManualRestoreRequests
              .FirstOrDefaultAsync(r => r.Id == reqId, cancellationToken);
          if (manualEntity != null)
          {
              manualEntity.Result = result.Success
                  ? JsonSerializer.Serialize(result.Summary)
                  : result.Error;
              manualEntity.Status = result.Success
                  ? ManualRestoreRequestStatus.Completed
                  : ManualRestoreRequestStatus.Failed;
          }
      }

      if (!result.Success)
      {
          if (manualEntity != null)
          {
              var audit = sp.GetRequiredService<IAuditLogService>();
              var actor = manualEntity.ApprovedByUserId ?? run.RequestedByUserId ?? "system";
              await ManualRestoreAudit.LogExecutionOutcomeAsync(
                  audit,
                  actor,
                  manualEntity,
                  succeeded: false,
                  run.CorrelationId);
          }

          await FailRunAsync(
              db,
              run,
              evidenceBuilder,
              details,
              RestoreVerificationStatus.Failed,
              "MANUAL_VALIDATION_RESTORE_FAILED",
              result.Error ?? "Validation restore failed.",
              RestoreDrillFailureCategory.PostRestoreContinuitySql,
              RestoreDrillStage.RestoreAttemptPassed,
              BuildBands(artifactResolved: true, pgList: true, isolated: false),
              null,
              null,
              null,
              null,
              null,
              null,
              null,
              cancellationToken);
          return true;
      }

      evidenceBuilder.Mark(RestoreDrillStage.PostRestoreContinuitySqlPassed, "manual_validation_restore_ok");
      run.RestoreDrillReachedStage = RestoreDrillStage.Completed;
      run.FiscalContinuityLayerPassed = result.Summary?.IntegrityChecksPassed;
      run.Status = RestoreVerificationStatus.Succeeded;
      run.CompletedAt = DateTime.UtcNow;
      run.FailureCode = null;
      run.FailureDetail = null;
      run.FailureCategory = RestoreDrillFailureCategory.None;
      if (run.StartedAt.HasValue)
          run.DurationMs = (long)(run.CompletedAt.Value - run.StartedAt.Value).TotalMilliseconds;
      run.DetailsJson = details.ToJsonString();
      run.EvidenceJson = RestoreDrillEvidenceJson.Serialize(new RestoreDrillEvidenceDocument
      {
          SchemaVersion = 5,
          CapturedAtUtc = DateTimeOffset.UtcNow,
          RestoreVerificationRunId = run.Id,
          SourceBackupRunId = run.SourceBackupRunId,
          SourceBackupArtifactId = run.SourceBackupArtifactId,
          Stages = evidenceBuilder.Stages
      });

      if (manualEntity != null)
      {
          var audit = sp.GetRequiredService<IAuditLogService>();
          var actor = manualEntity.ApprovedByUserId ?? run.RequestedByUserId ?? "system";
          await ManualRestoreAudit.LogExecutionOutcomeAsync(
              audit,
              actor,
              manualEntity,
              succeeded: true,
              run.CorrelationId);
      }

      await db.SaveChangesAsync(cancellationToken);
      _logger.LogInformation(
          "Manual validation restore succeeded: runId={RunId}, backupRunId={BackupRunId}, targetDb={TargetDb}",
          run.Id,
          backupRunId,
          targetDb);
      return true;
  }
}
