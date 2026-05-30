namespace KasseAPI_Final.Models.Backup;

/// <summary>Structured backup automation frequency (stored as UTC cron on <see cref="BackupScheduleConfiguration"/>).</summary>
public enum BackupScheduleFrequency
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Custom = 3,
}
