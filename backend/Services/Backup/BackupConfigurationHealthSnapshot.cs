using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupConfigurationHealthSnapshot
{
    public BackupConfigurationHealthLevel Level { get; init; }

    /// <summary>Operator-facing issues (English for logs/API).</summary>
    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

    public BackupExecutionAdapterKind EffectiveAdapterKind { get; init; }

    public bool WorkerEnabled { get; init; }

    /// <summary>True when <c>ExecutionAdapterKind=PgDump</c> (worker runs <c>pg_dump -Fc</c>).</summary>
    public bool RealPostgreSqlLogicalDumpConfigured { get; init; }

    /// <summary>Stable machine-readable execution profile (see BackupConfigurationEvaluation constants).</summary>
    public string BackupExecutionReality { get; init; } = string.Empty;

    /// <summary>
    /// When adapter is Fake or ProductionStub in a production-like environment and startup would pass, the configuration key that documents operator intent (English for operators).
    /// </summary>
    public string? NonRealBackupAdapterAcknowledgmentConfigurationKey { get; init; }

    /// <summary>One-line English summary for dashboards (complements <see cref="Issues"/>).</summary>
    public string ReadinessNarrative { get; init; } = string.Empty;

    /// <summary>Artifact vs restore disclaimer (all phases).</summary>
    public string ArtifactVerificationDisclaimer { get; init; } =
        "Artifact verification may include on-disk SHA-256 re-hash for produced files; it is still not restore verification, not pg_verifybackup, and does not prove RPO/RTO. TSE backup remains vendor-specific.";

    /// <summary>Saklama politikasının yürütülebilirlik özeti; silme varsayılan kapalıdır.</summary>
    public BackupRetentionReadinessSnapshot RetentionReadiness { get; init; } =
        BackupRetentionReadinessEvaluator.Build(new BackupOptions());
}
