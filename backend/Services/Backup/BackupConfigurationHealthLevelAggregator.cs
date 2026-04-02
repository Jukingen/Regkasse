namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Birleştirilmiş tanılardan sağlık seviyesini türetir (ör. pg_dump/pg_restore prob sonrası).
/// </summary>
public static class BackupConfigurationHealthLevelAggregator
{
    /// <summary>
    /// Mevcut seviyeyi, ek tanıların şiddetleriyle yükseltir (Error &gt; Warning &gt; bilgi yok).
    /// </summary>
    public static BackupConfigurationHealthLevel CombineWithDiagnostics(
        BackupConfigurationHealthLevel current,
        IEnumerable<BackupConfigurationDiagnostic> additional)
    {
        var bump = BackupConfigurationHealthLevel.Healthy;
        foreach (var d in additional)
        {
            if (d.Severity == BackupConfigurationDiagnosticSeverity.Error)
            {
                bump = BackupConfigurationHealthLevel.Unhealthy;
                break;
            }

            if (d.Severity == BackupConfigurationDiagnosticSeverity.Warning)
                bump = BackupConfigurationHealthLevel.Degraded;
        }

        return (BackupConfigurationHealthLevel)Math.Max((int)current, (int)bump);
    }
}
