using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupConfigurationHealthSnapshot
{
    public BackupConfigurationHealthLevel Level { get; init; }

    /// <summary>Operator-facing issues (English for logs/API).</summary>
    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

    public BackupExecutionAdapterKind EffectiveAdapterKind { get; init; }

    public bool WorkerEnabled { get; init; }

    /// <summary>Artifact vs restore disclaimer (all phases).</summary>
    public string ArtifactVerificationDisclaimer { get; init; } =
        "Artifact verification may include on-disk SHA-256 re-hash for produced files; it is still not restore verification, not pg_verifybackup, and does not prove RPO/RTO. TSE backup remains vendor-specific.";
}
