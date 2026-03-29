namespace KasseAPI_Final.Services.Backup;

public interface IBackupAlertPublisher
{
    void Publish(BackupAlertEvent evt);
}
