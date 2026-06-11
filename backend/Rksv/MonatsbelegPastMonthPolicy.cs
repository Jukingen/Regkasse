using KasseAPI_Final.Time;

namespace KasseAPI_Final.Rksv;

/// <summary>RKSV Monatsbeleg past-month guardrails (Vienna calendar month).</summary>
public static class MonatsbelegPastMonthPolicy
{
    public static int ComputeMonthDiff(int requestYear, int requestMonth, DateTime? utcNow = null)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow ?? DateTime.UtcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        var targetAnchor = new DateTime(requestYear, requestMonth, 1);
        var viennaAnchor = new DateTime(local.Year, local.Month, 1);
        return (viennaAnchor.Year - targetAnchor.Year) * 12 + (viennaAnchor.Month - targetAnchor.Month);
    }

    public static bool IsPastMonth(int requestYear, int requestMonth, DateTime? utcNow = null) =>
        ComputeMonthDiff(requestYear, requestMonth, utcNow) > 0;

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
