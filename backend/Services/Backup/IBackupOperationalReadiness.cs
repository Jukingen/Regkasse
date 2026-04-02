using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Exposes backup configuration / engine readiness for admin status surfaces.
/// </summary>
public interface IBackupOperationalReadiness
{
    BackupConfigurationHealthSnapshot GetConfigurationHealth();

    /// <summary>Veritabanı modunu okumadan yalnızca verilen admin modu için sağlık özeti (PUT doğrulaması).</summary>
    BackupConfigurationHealthSnapshot GetConfigurationHealthAssumingAdminMode(AdminBackupRuntimeExecutionMode adminMode);

    /// <summary>Salt config tabanlı artifact pipeline beklentisi (son runın durumu değil).</summary>
    BackupArtifactPipelinePolicySnapshot GetArtifactPipelinePolicy();
}
