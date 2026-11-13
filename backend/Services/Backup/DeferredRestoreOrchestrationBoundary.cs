namespace KasseAPI_Final.Services.Backup;

public sealed class DeferredRestoreOrchestrationBoundary : IRestoreOrchestrationBoundary
{
    public RestoreCapabilityDescriptor DescribeCapabilities() => new()
    {
        IsAutomatedRestoreAvailable = false,
        Notes =
            "Automated production restore is not available. " +
            "Super Admin validation-only restore uses dual approval + IRestoreService RKSV same-tenant gate " +
            "(see RestoreService / ManualRestoreTriggerService). " +
            "PostgreSQL PITR/pg_verifybackup automation and TSE vendor backup remain operator-led. " +
            "See docs/restore-boundary-notes.md."
    };
}
