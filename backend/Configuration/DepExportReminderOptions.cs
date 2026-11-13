namespace KasseAPI_Final.Configuration;

/// <summary>
/// Daily sweep that publishes FA activity (and configured email/webhook) reminders
/// for incomplete DEP export compliance requirements.
/// </summary>
public sealed class DepExportReminderOptions
{
    public const string SectionName = "DepExportReminder";

    /// <summary>When false, the hosted worker does not send reminders.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Hosted service delay between sweeps (hours). Minimum 1.</summary>
    public int CheckIntervalHours { get; set; } = 24;

    /// <summary>When true, only Legal requirements trigger reminders (skips Recommended/Optional).</summary>
    public bool LegalOnly { get; set; } = false;
}
