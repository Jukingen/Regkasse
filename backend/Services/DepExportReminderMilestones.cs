using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>Milestone buckets for DEP export due-date reminders.</summary>
public static class DepExportReminderMilestones
{
    public const string Days30 = "30d";
    public const string Days7 = "7d";
    public const string Days1 = "1d";
    public const string Overdue = "overdue";

    /// <summary>
    /// Calendar-day distance from <paramref name="utcNow"/> to <paramref name="dueDateUtc"/>.
    /// Negative means overdue.
    /// </summary>
    public static int DaysUntilDue(DateTime dueDateUtc, DateTime utcNow)
    {
        var due = dueDateUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dueDateUtc, DateTimeKind.Utc).Date
            : dueDateUtc.ToUniversalTime().Date;
        var now = utcNow.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).Date
            : utcNow.ToUniversalTime().Date;
        return (due - now).Days;
    }

    /// <summary>
    /// Returns the reminder milestone for a given day distance, or null when no reminder should fire.
    /// Matches the 30 / 7 / 1 day and overdue windows from the product sketch.
    /// </summary>
    public static string? ResolveMilestone(int daysUntilDue) =>
        daysUntilDue switch
        {
            30 => Days30,
            7 => Days7,
            1 => Days1,
            < 0 => Overdue,
            _ => null,
        };

    public static ActivityEventType EventTypeFor(string milestone) =>
        milestone == Overdue
            ? ActivityEventType.DepExportOverdue
            : ActivityEventType.DepExportDueSoon;

    public static string SeverityFor(string milestone) =>
        milestone == Overdue
            ? ActivitySeverityNames.Error
            : milestone == Days1
                ? ActivitySeverityNames.Warning
                : ActivitySeverityNames.Warning;

    public static string BuildGermanMessage(string milestone, DepExportRequirement requirement)
    {
        var due = requirement.DueDate?.ToString("dd.MM.yyyy") ?? "—";
        return milestone switch
        {
            Days30 =>
                $"In 30 Tagen fällig: {requirement.Title} (Frist {due}). Bitte den DEP-Export rechtzeitig erstellen.",
            Days7 =>
                $"In 7 Tagen fällig: {requirement.Title} (Frist {due}). Bitte den DEP-Export zeitnah erstellen.",
            Days1 =>
                $"Morgen fällig: {requirement.Title} (Frist {due}). Bitte den DEP-Export heute erstellen.",
            Overdue =>
                $"Überfällig: {requirement.Title} (Frist {due}). Bitte den DEP-Export umgehend nachholen.",
            _ => $"{requirement.Title} — DEP-Export Erinnerung (Frist {due}).",
        };
    }

    public static string BuildTitle(string milestone) =>
        milestone switch
        {
            Days30 => "DEP Export Erinnerung (30 Tage)",
            Days7 => "DEP Export Erinnerung (7 Tage)",
            Days1 => "DEP Export Erinnerung (morgen)",
            Overdue => "DEP Export überfällig",
            _ => "DEP Export Erinnerung",
        };

    /// <summary>
    /// Dedup key: one reminder per tenant / requirement identity / milestone.
    /// Overdue also includes the UTC calendar day so alerts can repeat daily while still overdue.
    /// </summary>
    public static string BuildDedupKey(
        Guid tenantId,
        DepExportRequirement requirement,
        string milestone,
        DateTime utcNow)
    {
        var period = requirement.PeriodStart?.ToString("yyyy-MM-dd") ?? "none";
        var category = string.IsNullOrWhiteSpace(requirement.Category)
            ? "unknown"
            : requirement.Category.Trim();
        var baseKey = $"dep-export-reminder:{tenantId:D}:{category}:{period}:{milestone}";
        if (milestone == Overdue)
            return $"{baseKey}:{utcNow.ToUniversalTime():yyyy-MM-dd}";
        return baseKey;
    }
}
