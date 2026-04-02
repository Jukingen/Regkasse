namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Machine-actionable backup setup signal (English messages; stable <see cref="Code"/> for UI/logs).
/// </summary>
public sealed class BackupConfigurationDiagnostic
{
    /// <summary>Stable identifier, e.g. BACKUP_SETUP_ADAPTER_FAKE.</summary>
    public required string Code { get; init; }

    public required BackupConfigurationDiagnosticSeverity Severity { get; init; }

    /// <summary>Human-readable English explanation (may duplicate an entry in <see cref="BackupConfigurationHealthSnapshot.Issues"/>).</summary>
    public required string Message { get; init; }

    /// <summary>Optional configuration keys to fix (appsettings / env) — not secrets.</summary>
    public IReadOnlyList<string>? RelatedConfigurationKeys { get; init; }
}
