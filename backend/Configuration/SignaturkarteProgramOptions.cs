namespace KasseAPI_Final.Configuration;

/// <summary>
/// Operational Mai 2027 Signaturkarte renewal program (independent of X.509 <c>ExpiresAt</c>).
/// </summary>
public sealed class SignaturkarteProgramOptions
{
    public const string SectionName = "SignaturkarteProgram";

    /// <summary>When false, reminders and FA urgency treat the program as inactive.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Program deadline (UTC). Default: 2027-05-31 end of Vienna day ≈ 21:59:59Z.</summary>
    public DateTime DeadlineUtc { get; set; } = new(2027, 5, 31, 21, 59, 59, DateTimeKind.Utc);

    /// <summary>Display label for FA / emails (not used for comparisons).</summary>
    public string DisplayName { get; set; } = "Mai 2027 Signaturkarte";

    /// <summary>Calendar-day anchors before <see cref="DeadlineUtc"/> that trigger reminders.</summary>
    public int[] ReminderDaysBefore { get; set; } = [180, 90, 30, 7];

    /// <summary>When true, soft/fake/demo providers are counted as Excluded (not Open).</summary>
    public bool ExcludeDemoAndSoftDevices { get; set; } = true;

    /// <summary>When true, only explicit CompliantAtUtc marks compliance (IssuedAt auto-rule deferred).</summary>
    public bool RequireExplicitComplianceFlag { get; set; } = true;

    /// <summary>Hosted service delay between sweeps (hours). Minimum 1.</summary>
    public int CheckIntervalHours { get; set; } = 24;

    /// <summary>Also fire daily overdue reminders after the deadline while Open devices remain.</summary>
    public bool SendOverdueReminders { get; set; } = true;
}
