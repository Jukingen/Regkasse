namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Observability severity for <see cref="BackupConfigurationDiagnostic"/> (logs / admin API; not process exit codes).
/// </summary>
public enum BackupConfigurationDiagnosticSeverity
{
    Information = 0,

    Warning = 1,

    Error = 2
}
