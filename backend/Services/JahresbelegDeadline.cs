namespace KasseAPI_Final.Services;

/// <summary>
/// Vienna-calendar Jahresbeleg creation reminder. The warning goes out on 1 December.
/// The receipt for that calendar year is due by 31 January of the following year.
/// </summary>
public static class JahresbelegDeadline
{
    public const string WarningMessage = "Jahresbeleg deadline is January 31. Please create it.";

    public static bool IsReminderDay(DateTime viennaLocal) =>
        viennaLocal.Month == 12 && viennaLocal.Day == 1;

    /// <summary>
    /// Year the Jahresbeleg belongs to. In January the open obligation is still the previous year.
    /// </summary>
    public static int YearDue(DateTime viennaLocal) =>
        viennaLocal.Month == 1 ? viennaLocal.Year - 1 : viennaLocal.Year;

    public static DateOnly Deadline(int yearDue) => new(yearDue + 1, 1, 31);

    public static bool IsInReminderWindow(DateTime viennaLocal)
    {
        var yearDue = YearDue(viennaLocal);
        var start = new DateTime(yearDue, 12, 1);
        var deadline = Deadline(yearDue).ToDateTime(TimeOnly.MinValue);
        return viennaLocal.Date >= start.Date && viennaLocal.Date <= deadline.Date;
    }

    public static string DedupKey(Guid tenantId, int yearDue) =>
        $"jahresbeleg-deadline:{tenantId:D}:{yearDue}";
}
