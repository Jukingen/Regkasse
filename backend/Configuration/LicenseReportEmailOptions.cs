namespace KasseAPI_Final.Configuration;

/// <summary>Optional scheduled emails for issued-license inventory (uses <see cref="EmailSmtpOptions"/> SMTP).</summary>
public sealed class LicenseReportEmailOptions
{
    public const string SectionName = "License:ReportEmail";

    /// <summary>Send a weekly plaintext summary (issued counts, expiring buckets).</summary>
    public bool EnableWeeklySummary { get; set; }

    /// <summary>Send a digest when any active issued license hits 30 / 15 / 7 calendar days before expiry.</summary>
    public bool EnableIssuedExpiryAlerts { get; set; }

    /// <summary>Day-of-week to send the weekly summary (UTC): 0 = Sunday, 1 = Monday, … 6 = Saturday.</summary>
    public int WeeklySummaryDayOfWeekUtc { get; set; } = 1;

    /// <summary>Hour (0–23) UTC when scheduled report jobs run.</summary>
    public int RunHourUtc { get; set; } = 6;

    /// <summary>Minute (0–59) UTC when scheduled report jobs run.</summary>
    public int RunMinuteUtc { get; set; } = 0;
}
