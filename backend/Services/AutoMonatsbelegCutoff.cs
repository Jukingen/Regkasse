using KasseAPI_Final.Time;

namespace KasseAPI_Final.Services;

/// <summary>
/// Vienna-local window for automatic previous-month Monatsbeleg creation
/// (1st 00:01 + product catch-up through day 14; RKSV legal window remains 7 days).
/// </summary>
public static class AutoMonatsbelegCutoff
{
    public const int DefaultCatchUpThroughDay = 14;
    public const int MinRetryCount = 1;
    public const int MaxRetryCount = 5;
    public const int DefaultRetryCount = 3;

    /// <summary>Vienna local time on the 1st of the month when auto-create becomes eligible.</summary>
    public static readonly TimeSpan FirstOfMonthLocalTime = TimeSpan.FromMinutes(1);

    public const string SystemActorUserId = "system";
    public const string SystemActorRole = "System";

    public static DateTime ToVienna(DateTime utcNow)
    {
        var utc = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, PostgreSqlUtcDateTime.AustriaTimeZone);
    }

    public static bool IsInAutoCreateWindow(DateTime viennaNow, int catchUpThroughDay = DefaultCatchUpThroughDay)
    {
        var through = Math.Clamp(catchUpThroughDay, 1, 31);
        if (viennaNow.Day > through)
            return false;
        if (viennaNow.Day == 1 && viennaNow.TimeOfDay < FirstOfMonthLocalTime)
            return false;
        return true;
    }

    public static DateTime PreviousMonthAnchor(DateTime viennaNow) =>
        new DateTime(viennaNow.Year, viennaNow.Month, 1).AddMonths(-1);

    public static int ClampRetryCount(int value) => Math.Clamp(value, MinRetryCount, MaxRetryCount);

    /// <summary>In-sweep exponential backoff: attempt 1 → 1s, 2 → 2s, 3 → 4s.</summary>
    public static TimeSpan RetryBackoff(int attemptNumber)
    {
        var exp = Math.Clamp(attemptNumber - 1, 0, 6);
        return TimeSpan.FromSeconds(Math.Pow(2, exp));
    }

    /// <summary>FinanzOnline Belegcheck deadline for a December/Jahresbeleg covering <paramref name="jahresbelegYear"/>.</summary>
    public static DateTime JahresbelegFonDeadline(int jahresbelegYear) =>
        new(jahresbelegYear + 1, 2, 15);

    /// <summary>
    /// Hosted sweep delay. When <paramref name="allowImmediateIfInWindow"/> and already past 00:01 on day 1–14, returns zero.
    /// On day 1 before 00:01, waits until 00:01 (capped by the poll interval).
    /// </summary>
    public static TimeSpan GetDelayUntilNextSweep(
        DateTime utcNow,
        int intervalMinutes,
        bool allowImmediateIfInWindow)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(5, intervalMinutes));
        var vienna = ToVienna(utcNow);
        if (allowImmediateIfInWindow && IsInAutoCreateWindow(vienna))
            return TimeSpan.Zero;
        if (vienna.Day == 1 && vienna.TimeOfDay < FirstOfMonthLocalTime)
        {
            var until = FirstOfMonthLocalTime - vienna.TimeOfDay;
            return until < interval ? until : interval;
        }

        return interval;
    }

    public static string MaskTseSignature(string? jws)
    {
        if (string.IsNullOrWhiteSpace(jws))
            return string.Empty;
        var s = jws.Trim();
        if (s.Length <= 20)
            return s;
        return string.Concat(s.AsSpan(0, 8), "…", s.AsSpan(s.Length - 8));
    }

    public static bool IsCompactJws(string? jws)
    {
        if (string.IsNullOrWhiteSpace(jws))
            return false;
        var dots = 0;
        foreach (var c in jws)
        {
            if (c == '.')
                dots++;
        }

        return dots == 2;
    }

    public static string DepStatus(string? jws) =>
        IsCompactJws(jws) ? "InDep" : "Missing";
}
