using System.Globalization;
using System.Text;
using KasseAPI_Final.Models;
using KasseAPI_Final.Time;

namespace KasseAPI_Final.Services.Reports;

/// <summary>
/// Plain-text / PDF notice for backdated (nachträglicher) Tagesabschluss reports.
/// Keeps late creation obvious for Betriebsprüfung without forging fiscal timestamps.
/// </summary>
public static class DailyClosingBackdatedReportNote
{
    private static readonly CultureInfo DeAt = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>ASCII rule. Box-drawing U+2550 is not in the bundled Lato face.</summary>
    private const string Rule = "===========================================";

    public static string? TryFormat(DailyClosing closing)
    {
        ArgumentNullException.ThrowIfNull(closing);
        if (!closing.IsBackdated)
            return null;

        return Format(
            closing.ClosingDate,
            closing.CreatedAt,
            closing.LateCreationReason);
    }

    public static string Format(DateTime closingDate, DateTime createdAt, string? reason)
    {
        var originalLocal = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(closingDate);
        var createdLocal = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(createdAt);
        var grund = string.IsNullOrWhiteSpace(reason) ? "—" : reason.Trim();

        var builder = new StringBuilder();
        builder.AppendLine(Rule);
        builder.AppendLine("Hinweis: Dieser Tagesabschluss wurde verspätet erstellt.");
        builder.AppendLine($"Ursprüngliches Datum: {originalLocal.ToString("dd.MM.yyyy", DeAt)}");
        builder.AppendLine($"Erstellt am: {createdLocal.ToString("dd.MM.yyyy", DeAt)}");
        builder.AppendLine($"Grund: {grund}");
        builder.Append(Rule);
        return builder.ToString();
    }
}
