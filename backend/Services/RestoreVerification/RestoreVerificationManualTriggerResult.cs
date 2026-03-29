using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class RestoreVerificationManualTriggerResult
{
    public required RestoreVerificationRun Run { get; init; }

    public RestoreVerificationTriggerOrchestrationState OrchestrationState { get; init; }

    public bool NewQueuedRunCreated =>
        OrchestrationState == RestoreVerificationTriggerOrchestrationState.NewlyQueued;

    public bool ExistingRunReturned =>
        OrchestrationState != RestoreVerificationTriggerOrchestrationState.NewlyQueued;
}
