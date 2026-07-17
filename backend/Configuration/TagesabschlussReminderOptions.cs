namespace KasseAPI_Final.Configuration;

/// <summary>
/// Evening reminder when Tagesabschluss is still pending (never auto-closes — RKSV requires manual closing).
/// </summary>
public sealed class TagesabschlussReminderOptions
{
    public const string SectionName = "TagesabschlussReminder";

    /// <summary>When false, the hosted worker does not send reminders.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Europe/Vienna local hour to start the reminder window (default 22:00).</summary>
    public int ReminderHourVienna { get; set; } = 22;

    /// <summary>How long after <see cref="ReminderHourVienna"/> reminders may be sent (default 2 hours → 22:00–24:00).</summary>
    public int WindowHours { get; set; } = 2;

    /// <summary>Hosted service polling interval in minutes.</summary>
    public int CheckIntervalMinutes { get; set; } = 60;

    /// <summary>Only remind when the register has at least one fiscal payment today (Vienna day).</summary>
    public bool RequireTransactions { get; set; } = true;
}
