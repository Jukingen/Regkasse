namespace KasseAPI_Final.Configuration;

/// <summary>Background Monatsbeleg missing reminders and optional auto-create.</summary>
public sealed class MonatsbelegOpsOptions
{
    public const string SectionName = "MonatsbelegOps";

    /// <summary>When false, the hosted worker does not send daily missing-Monatsbeleg reminders.</summary>
    public bool ReminderEnabled { get; set; } = true;

    /// <summary>
    /// Master switch for Auto-Monatsbeleg. Each tenant still needs
    /// <c>CompanySettings.AutoMonatsbelegEnabled</c>.
    /// </summary>
    public bool AutoCreateEnabled { get; set; } = true;

    /// <summary>Legacy poll interval in hours. Used only when <see cref="CheckIntervalMinutes"/> is unset.</summary>
    public int CheckIntervalHours { get; set; } = 6;

    /// <summary>Hosted poll interval in minutes (minimum 5). Default 15. Wins over <see cref="CheckIntervalHours"/>.</summary>
    public int CheckIntervalMinutes { get; set; } = 15;

    /// <summary>Vienna calendar day (inclusive) through which a missing previous-month receipt is still auto-created.</summary>
    public int CatchUpThroughDay { get; set; } = 7;

    /// <summary>When false, in-sweep retries do not sleep (tests).</summary>
    public bool RetryBackoffEnabled { get; set; } = true;
}
