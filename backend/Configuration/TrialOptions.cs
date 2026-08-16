namespace KasseAPI_Final.Configuration;

/// <summary>SaaS mandant trial duration, reminders, grace, and soft cleanup.</summary>
public sealed class TrialOptions
{
    public const string SectionName = "Trial";

    /// <summary>Default trial length when Super Admin does not override (days).</summary>
    public int DefaultDurationDays { get; set; } = 14;

    /// <summary>Days after trial end before operational lockdown escalates (login still allowed with banner).</summary>
    public int GracePeriodDays { get; set; } = 7;

    /// <summary>
    /// Days after grace end when expired trial tenants are soft-archived
    /// (<c>trial_status=deleted</c> + tenant soft-delete). Does not hard-wipe RKSV data.
    /// </summary>
    public int AutoDeleteAfterGraceDays { get; set; } = 30;

    /// <summary>Exact day-remaining anchors for reminder emails (default 7 / 3 / 1).</summary>
    public int[] ReminderDays { get; set; } = [7, 3, 1];

    /// <summary>When false, trial grant/reminders/cleanup hosted jobs no-op.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Default for demo catalog import when creating a trial tenant (wizard may override).</summary>
    public bool DemoCatalogImport { get; set; } = true;

    /// <summary>Enforced max cash registers while trial is open (active/expired grace).</summary>
    public int MaxRegistersInTrial { get; set; } = 1;

    /// <summary>Enforced max active user memberships while trial is open.</summary>
    public int MaxUsersInTrial { get; set; } = 3;

    /// <summary>Allowed override durations for Super Admin create wizard.</summary>
    public int[] AllowedDurationDays { get; set; } = [14, 30, 60, 90];

    /// <summary>Hosted reminder sweep interval hours (default 6).</summary>
    public int ReminderIntervalHours { get; set; } = 6;

    /// <summary>UTC hour for daily cleanup tick (default 02:00).</summary>
    public int CleanupHourUtc { get; set; } = 2;

    /// <summary>UTC minute for daily cleanup tick.</summary>
    public int CleanupMinuteUtc { get; set; } = 0;
}
