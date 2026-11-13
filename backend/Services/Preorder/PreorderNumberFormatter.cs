namespace KasseAPI_Final.Services.Preorder;

/// <summary>Operational Besorgerzettel numbers: BS + yyMMdd + daily sequence (not a fiscal BelegNr).</summary>
public static class PreorderNumberFormatter
{
    public const string Prefix = "BS";

    public static DateTime ViennaDate(DateTime utcNow)
    {
        var utc = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, ResolveVienna()).Date;
    }

    public static string BuildPrefix(DateTime viennaDate) => $"{Prefix}{viennaDate:yyMMdd}";

    public static string Format(DateTime viennaDate, int sequence) => $"{BuildPrefix(viennaDate)}{sequence}";

    private static TimeZoneInfo ResolveVienna()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        }
    }
}
