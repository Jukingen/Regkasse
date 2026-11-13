using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>English API labels for backup actors (FA maps cron to i18n).</summary>
public static class BackupRunActorLabels
{
    public const string SystemCron = "System (Cron)";

    public static bool IsScheduledOrSystemActor(BackupTriggerSource trigger, string? requestedByUserId) =>
        trigger == BackupTriggerSource.Scheduled || string.IsNullOrWhiteSpace(requestedByUserId);

    public static string Resolve(BackupTriggerSource trigger, string? displayName, string? requestedByUserId)
    {
        if (IsScheduledOrSystemActor(trigger, requestedByUserId))
            return SystemCron;
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName.Trim();
        return requestedByUserId!.Trim();
    }
}
