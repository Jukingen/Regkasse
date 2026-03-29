namespace KasseAPI_Final.Services.Backup;

public sealed class DeferredRestoreOrchestrationBoundary : IRestoreOrchestrationBoundary
{
    public RestoreCapabilityDescriptor DescribeCapabilities() => new()
    {
        IsAutomatedRestoreAvailable = false,
        Notes =
            "Phase 1 delivers backup orchestration metadata and verification scaffolding only. " +
            "PostgreSQL PITR/pg_verifybackup-based restore automation, monthly restore drills, and TSE vendor backup remain operator-led / deferred. " +
            "See docs/restore-boundary-notes.md."
    };
}
