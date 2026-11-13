namespace KasseAPI_Final.Configuration;

/// <summary>
/// Weekly Backup/DR drill summary email. SMTP stays on <see cref="EmailSmtpOptions"/>.
/// Defaults: disabled, Monday 06:00 UTC. An empty <see cref="Recipients"/> skips the send.
/// </summary>
public sealed class BackupDrillReportOptions
{
    public const string SectionName = "BackupDrillReport";

    public bool Enabled { get; set; }

    /// <summary>UTC day of week: 0 = Sunday, 1 = Monday, … 6 = Saturday.</summary>
    public int DayOfWeekUtc { get; set; } = 1;

    public int RunHourUtc { get; set; } = 6;

    public int RunMinuteUtc { get; set; }

    /// <summary>Comma- or semicolon-separated addresses. Empty skips the send.</summary>
    public string Recipients { get; set; } = string.Empty;
}
