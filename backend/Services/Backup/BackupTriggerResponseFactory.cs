using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

public static class BackupTriggerResponseFactory
{
    public static BackupTriggerResponseDto Create(
        BackupManualTriggerOutcome outcome,
        BackupArtifactPipelinePolicySnapshot? pipelinePolicy = null,
        int? automaticRetryMaxAttemptsBudget = null)
    {
        var (state, newQueued, duplicatePrevented) = outcome.Kind switch
        {
            BackupManualTriggerResultKind.NewRunQueued => ("NEW_RUN_QUEUED_AWAITING_WORKER", true, false),
            BackupManualTriggerResultKind.IdempotentReplay => ("IDEMPOTENT_REPLAY_EXISTING_RUN", false, false),
            BackupManualTriggerResultKind.DuplicateActiveManualPrevented => ("DUPLICATE_ACTIVE_MANUAL_PREVENTED", false, true),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind), outcome.Kind, null)
        };

        return new BackupTriggerResponseDto
        {
            Run = BackupRunMapper.ToDto(
                outcome.Run,
                includeChildren: false,
                duplicateExecutionPreventedOverride: duplicatePrevented ? true : null,
                pipelinePolicy: pipelinePolicy,
                materializedChildren: false,
                automaticRetryMaxAttemptsBudget: automaticRetryMaxAttemptsBudget),
            DuplicateExecutionPrevented = duplicatePrevented,
            NewQueuedRunCreated = newQueued,
            OrchestrationState = state
        };
    }
}
