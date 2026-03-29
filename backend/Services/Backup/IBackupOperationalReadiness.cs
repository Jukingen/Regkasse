namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Exposes backup configuration / engine readiness for admin status surfaces.
/// </summary>
public interface IBackupOperationalReadiness
{
    BackupConfigurationHealthSnapshot GetConfigurationHealth();

    /// <summary>Salt config tabanlı artifact pipeline beklentisi (son runın durumu değil).</summary>
    BackupArtifactPipelinePolicySnapshot GetArtifactPipelinePolicy();
}
