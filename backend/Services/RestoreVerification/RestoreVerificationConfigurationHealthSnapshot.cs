namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class RestoreVerificationConfigurationHealthSnapshot
{
    public RestoreVerificationConfigurationHealthLevel Level { get; init; }

    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

    public bool WorkerEnabled { get; init; }

    public bool OrchestratorDistributedLockEnabled { get; init; }

    /// <summary>Restore confidence drill ≠ artifact checksum (admin UI disclaimer).</summary>
    public string ScopeDisclaimer { get; init; } =
        "Restore verification drills are not backup artifact checksum verification; see backup admin APIs for artifact pipeline.";
}
