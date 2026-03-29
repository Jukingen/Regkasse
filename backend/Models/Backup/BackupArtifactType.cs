namespace KasseAPI_Final.Models.Backup;

public enum BackupArtifactType
{
    LogicalDump = 0,
    PhysicalBaseBackup = 1,
    WalArchiveWindow = 2,
    GlobalsDump = 3,
    VerificationManifest = 4,
    BackupLog = 5,
}
