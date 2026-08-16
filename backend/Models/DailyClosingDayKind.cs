namespace KasseAPI_Final.Models;

/// <summary>
/// Kind of a daily (Vienna business day) closing. Independent of
/// <see cref="DailyClosing.ClosingType"/> (Daily / Monthly / Yearly) and of
/// <see cref="DailyClosing.IsBackdated"/> — an empty holiday closing may also be nachträglich.
/// </summary>
public enum DailyClosingDayKind
{
    Normal,
    Empty,
}

/// <summary>Persisted <see cref="DailyClosing.DayKind"/> values (varchar, lowercase).</summary>
public static class DailyClosingDayKinds
{
    public const string Normal = "normal";
    public const string Empty = "empty";

    public static string FromTransactionCount(int transactionCount) =>
        transactionCount == 0 ? Empty : Normal;

    public static bool IsEmptyValue(string? dayKind) =>
        string.Equals(dayKind, Empty, StringComparison.OrdinalIgnoreCase);

    public static DailyClosingDayKind Parse(string? value) =>
        IsEmptyValue(value) ? DailyClosingDayKind.Empty : DailyClosingDayKind.Normal;
}
