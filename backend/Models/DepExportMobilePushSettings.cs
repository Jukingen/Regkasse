namespace KasseAPI_Final.Models;

/// <summary>
/// Tenant-scoped mobile push toggles for DEP export reminder milestones.
/// Stored inside <see cref="NotificationConfig"/> JSON (no separate migration).
/// </summary>
public sealed class DepExportMobilePushSettings
{
    /// <summary>Master switch for DEP mobile push delivery.</summary>
    public bool PushEnabled { get; set; } = true;

    public bool ThirtyDayReminder { get; set; } = true;

    public bool SevenDayReminder { get; set; } = true;

    public bool OneDayReminder { get; set; } = true;

    public bool OverdueAlert { get; set; } = true;

    /// <summary>Notify tenant admins when a DEP export completes successfully.</summary>
    public bool SuccessNotification { get; set; } = true;

    public static DepExportMobilePushSettings CreateDefault() => new();

    /// <summary>
    /// Whether the given reminder milestone may send push
    /// (<see cref="Services.DepExportReminderMilestones"/> constants).
    /// </summary>
    public bool IsMilestoneEnabled(string milestone)
    {
        if (!PushEnabled)
            return false;

        return milestone switch
        {
            "30d" => ThirtyDayReminder,
            "7d" => SevenDayReminder,
            "1d" => OneDayReminder,
            "overdue" => OverdueAlert,
            _ => false,
        };
    }
}
