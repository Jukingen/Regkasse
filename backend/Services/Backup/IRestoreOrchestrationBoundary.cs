namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Phase 2+: automated restore drill / PITR orchestration. Phase 1: capability descriptor only — no destructive operations.
/// </summary>
public interface IRestoreOrchestrationBoundary
{
    RestoreCapabilityDescriptor DescribeCapabilities();
}

public sealed class RestoreCapabilityDescriptor
{
    public bool IsAutomatedRestoreAvailable { get; init; }

    /// <summary>Operator-facing notes (e.g. TSE vendor backup still required).</summary>
    public string Notes { get; init; } = string.Empty;
}
