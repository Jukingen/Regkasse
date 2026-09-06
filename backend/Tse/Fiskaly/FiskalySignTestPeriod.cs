using KasseAPI_Final.DTOs;
using KasseAPI_Final.Time;

namespace KasseAPI_Final.Tse.Fiskaly;

/// <summary>Resolves Vienna calendar year/month for Development sign-test Sonderbelege.</summary>
public static class FiskalySignTestPeriod
{
    public const string PeriodNotCompletedMessage =
        "Monatsbeleg can only be created for completed (past) Vienna calendar months.";

    public static bool TryResolve(
        FiskalySignTestRequest request,
        bool yearly,
        out int year,
        out int month,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(request);
        year = 0;
        month = 0;
        errorMessage = null;

        var viennaLocal = ResolveViennaLocal(request.ClosingDate);
        var (anchorYear, anchorMonth) = request.ClosingDate is null
            ? PostgreSqlUtcDateTime.GetViennaCurrentYearMonth()
            : (viennaLocal.Year, viennaLocal.Month);
        var previous = new DateTime(anchorYear, anchorMonth, 1).AddMonths(-1);

        if (yearly)
        {
            year = request.Year ?? anchorYear - 1;
            month = 1;
        }
        else if (request.Year is int explicitYear && request.Month is int explicitMonth)
        {
            year = explicitYear;
            month = explicitMonth;
        }
        else
        {
            year = request.Year ?? previous.Year;
            month = request.Month ?? previous.Month;
        }

        if (year is < 2000 or > 2100)
        {
            errorMessage = "Year must be between 2000 and 2100.";
            return false;
        }

        if (!yearly && month is < 1 or > 12)
        {
            errorMessage = "Month must be between 1 and 12.";
            return false;
        }

        if (!yearly)
        {
            var (nowYear, nowMonth) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
            if (new DateTime(year, month, 1) >= new DateTime(nowYear, nowMonth, 1))
            {
                errorMessage = PeriodNotCompletedMessage;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Maps <paramref name="closingDate"/> to Europe/Vienna wall time.
    /// Unspecified values are treated as UTC (ISO payloads from the admin UI).
    /// </summary>
    public static DateTime ResolveViennaLocal(DateTime? closingDate)
    {
        var utc = closingDate is { } value
            ? value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            }
            : DateTime.UtcNow;

        return TimeZoneInfo.ConvertTimeFromUtc(utc, PostgreSqlUtcDateTime.AustriaTimeZone);
    }
}
