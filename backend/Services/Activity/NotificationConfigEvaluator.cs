using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Activity;

internal static class NotificationConfigEvaluator
{
    public static bool IsEventTypeEnabled(NotificationConfig config, ActivityEventType type)
    {
        if (config.EnabledEvents.Count == 0)
            return true;

        return config.EnabledEvents.GetValueOrDefault(type, true);
    }

    public static bool MeetsSeverityThreshold(NotificationConfig config, ActivityEventType type, string severity)
    {
        if (!config.SeverityThreshold.TryGetValue(type, out var threshold) || string.IsNullOrWhiteSpace(threshold))
            return true;

        return ActivityEventSeverityRules.MeetsMinimum(
            severity,
            ActivitySeverityNames.NormalizeOrDefault(threshold, ActivitySeverityNames.Info));
    }

    public static bool ShouldDeliverInApp(NotificationConfig config, ActivityEventType type, string severity) =>
        config.InAppEnabled
        && IsEventTypeEnabled(config, type)
        && MeetsSeverityThreshold(config, type, severity);

    public static bool RequiresImmediateCriticalEmail(ActivityEventType type) =>
        type is ActivityEventType.BackupFailed or ActivityEventType.LicenseExpired;

    public static bool ShouldDeliverEmail(NotificationConfig config, ActivityEventType type, string severity)
    {
        if (!IsEventTypeEnabled(config, type))
            return false;

        if (RequiresImmediateCriticalEmail(type))
            return HasEmailRecipients(config);

        return config.EmailEnabled
               && HasEmailRecipients(config)
               && MeetsSeverityThreshold(config, type, severity);
    }

    private static bool HasEmailRecipients(NotificationConfig config) =>
        config.EmailRecipients.Count > 0;

    public static bool ShouldDeliverWebhook(NotificationConfig config, ActivityEventType type, string severity) =>
        config.WebhookEnabled
        && !string.IsNullOrWhiteSpace(config.WebhookUrl)
        && IsEventTypeEnabled(config, type)
        && MeetsSeverityThreshold(config, type, severity);
}
