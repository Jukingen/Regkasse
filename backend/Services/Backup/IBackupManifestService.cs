namespace KasseAPI_Final.Services.Backup;

/// <summary>Produces manifest JSON for backup runs (does not replace backup files).</summary>
public interface IBackupManifestService
{
    BackupManifestDocument BuildLogicalPgDumpManifest(BackupLogicalManifestInput input);
}
