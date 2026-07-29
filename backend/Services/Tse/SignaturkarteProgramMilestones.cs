using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Tse;

/// <summary>Milestone helpers for Mai 2027 Signaturkarte program reminders.</summary>
public static class SignaturkarteProgramMilestones
{
    public const string Overdue = "overdue";

    /// <summary>Calendar-day distance from <paramref name="utcNow"/> to <paramref name="deadlineUtc"/>.</summary>
    public static int DaysUntilDeadline(DateTime deadlineUtc, DateTime utcNow)
    {
        var due = deadlineUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(deadlineUtc, DateTimeKind.Utc).Date
            : deadlineUtc.ToUniversalTime().Date;
        var now = utcNow.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).Date
            : utcNow.ToUniversalTime().Date;
        return (due - now).Days;
    }

    /// <summary>
    /// Returns a configured reminder milestone label when <paramref name="daysUntilDeadline"/>
    /// matches an anchor, or <see cref="Overdue"/> when past deadline and overdue reminders are enabled.
    /// </summary>
    public static string? ResolveMilestone(
        int daysUntilDeadline,
        IReadOnlyCollection<int> reminderDaysBefore,
        bool sendOverdue)
    {
        if (daysUntilDeadline < 0)
            return sendOverdue ? Overdue : null;

        foreach (var day in reminderDaysBefore)
        {
            if (day == daysUntilDeadline)
                return $"{day}d";
        }

        return null;
    }

    public static ActivityEventType EventTypeFor(string milestone) =>
        milestone == Overdue
            ? ActivityEventType.SignaturkarteProgramOverdue
            : ActivityEventType.SignaturkarteProgramReminder;

    public static string SeverityFor(string milestone, int daysUntilDeadline) =>
        milestone == Overdue || daysUntilDeadline <= 7
            ? ActivitySeverityNames.Error
            : daysUntilDeadline <= 90
                ? ActivitySeverityNames.Warning
                : ActivitySeverityNames.Info;

    public static string BuildTitle(string milestone, string displayName) =>
        milestone == Overdue
            ? $"{displayName} — überfällig"
            : $"{displayName} Erinnerung ({milestone})";

    public static string BuildGermanMessage(
        string milestone,
        int daysUntilDeadline,
        int openDeviceCount,
        DateTime deadlineUtc)
    {
        var deadline = deadlineUtc.ToUniversalTime().ToString("dd.MM.yyyy");
        if (milestone == Overdue)
        {
            return
                $"Mai 2027 Signaturkarte-Pflicht überfällig (Deadline {deadline}). " +
                $"{openDeviceCount} Gerät(e) noch offen — unabhängig vom Zertifikatsablauf.";
        }

        return
            $"Mai 2027 Signaturkarte-Pflicht — noch {daysUntilDeadline} Tag(e) bis {deadline}. " +
            $"{openDeviceCount} Gerät(e) offen. Unabhängig vom Zertifikatsablauf.";
    }

    /// <summary>
    /// Dedup: one reminder per deadline / days-or-overdue / tenant (or platform) scope.
    /// Overdue includes UTC calendar day so alerts can repeat daily.
    /// </summary>
    public static string BuildDedupKey(
        DateTime deadlineUtc,
        string milestone,
        string scope,
        DateTime utcNow)
    {
        var deadlineStamp = deadlineUtc.ToUniversalTime().ToString("yyyyMMdd");
        var baseKey = $"signaturkarte-program:{deadlineStamp}:{milestone}:{scope}";
        if (milestone == Overdue)
            return $"{baseKey}:{utcNow.ToUniversalTime():yyyy-MM-dd}";
        return baseKey;
    }

    /// <summary>FA banner severity bucket from days remaining (null when program inactive).</summary>
    public static string? BannerSeverity(int daysRemaining, int openCount)
    {
        if (openCount <= 0)
            return null;
        if (daysRemaining <= 7)
            return "critical";
        if (daysRemaining <= 90)
            return "warning";
        return "info";
    }
}
