using System.Globalization;

namespace KasseAPI_Final.Services;

internal static class SystemTimeViennaFormatter
{
    public static string FormatUtcAsViennaWallClock(DateTime utcNow)
    {
        utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        try
        {
            var tzId = OperatingSystem.IsWindows() ? "W. Europe Standard Time" : "Europe/Vienna";
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);
            return local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
        catch
        {
            return utcNow.ToString("u", CultureInfo.InvariantCulture);
        }
    }
}
