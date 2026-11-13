namespace KasseAPI_Final.Services.Reports;

/// <summary>
/// Z-report notice when a closing continued while tables or carts were still open.
/// The fiscal total stays the paid-invoice sum.
/// </summary>
public static class DailyClosingOpenTablesReportNote
{
    public static string? TryFormat(bool openOrdersForced, int openOrdersCount)
    {
        if (!openOrdersForced || openOrdersCount <= 0)
            return null;

        return
            $"Hinweis: {openOrdersCount} offene Tische/Warenkörbe wurden mit Warnung geschlossen (ForceWithWarning). Unbezahlte Beträge sind nicht in der Summe enthalten.";
    }
}
