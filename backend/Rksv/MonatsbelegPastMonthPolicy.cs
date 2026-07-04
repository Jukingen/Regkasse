using KasseAPI_Final.Time;

namespace KasseAPI_Final.Rksv;

/// <summary>RKSV Monatsbeleg past-month guardrails (Vienna calendar month).</summary>
public static class MonatsbelegPastMonthPolicy
{
    /// <summary>
    /// RKSV grace window (days after month end) within which a Monatsbeleg is still considered on-time.
    /// See AGENTS.md: "Monatsbeleg MUST be created within 7 days of month end."
    /// </summary>
    public const int MonatsbelegGraceDays = 7;

    public static int ComputeMonthDiff(int requestYear, int requestMonth, DateTime? utcNow = null)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow ?? DateTime.UtcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        var targetAnchor = new DateTime(requestYear, requestMonth, 1);
        var viennaAnchor = new DateTime(local.Year, local.Month, 1);
        return (viennaAnchor.Year - targetAnchor.Year) * 12 + (viennaAnchor.Month - targetAnchor.Month);
    }

    public static bool IsPastMonth(int requestYear, int requestMonth, DateTime? utcNow = null) =>
        ComputeMonthDiff(requestYear, requestMonth, utcNow) > 0;

    /// <summary>
    /// Whole days a Monatsbeleg is late, measured from the legal deadline
    /// (end of the target month + <see cref="MonatsbelegGraceDays"/> grace days) to "now" in the Vienna calendar.
    /// Returns 0 when created on time or early. This describes lateness only — it never affects the receipt's
    /// real creation/signing timestamp.
    /// </summary>
    public static int ComputeDaysLate(int requestYear, int requestMonth, DateTime? utcNow = null)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow ?? DateTime.UtcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        var deadline = new DateTime(requestYear, requestMonth, DateTime.DaysInMonth(requestYear, requestMonth))
            .AddDays(MonatsbelegGraceDays);
        var days = (local.Date - deadline.Date).Days;
        return days < 0 ? 0 : days;
    }

    /// <summary>True when the Monatsbeleg for the target period is created past its legal deadline (verspätet).</summary>
    public static bool IsLateCreation(int requestYear, int requestMonth, DateTime? utcNow = null) =>
        ComputeDaysLate(requestYear, requestMonth, utcNow) > 0;

    public static string BuildWarningMessage(int monthDiff) =>
        monthDiff switch
        {
            1 => "Monatsbeleg für den Vormonat wird erstellt. Dies ist zulässig.",
            <= 6 => $"Monatsbeleg für {monthDiff} Monate zurück wird erstellt. FinanzOnline akzeptiert dies in der Regel.",
            _ => $"Monatsbeleg für {monthDiff} Monate zurück wird erstellt. Dies könnte bei einer Betriebsprüfung hinterfragt werden. Nur mit Steuerberater-Rücksprache fortfahren.",
        };

    public static string ResolveSeverity(int monthDiff) =>
        monthDiff <= 1 ? "info" : monthDiff <= 6 ? "warning" : "error";
}
