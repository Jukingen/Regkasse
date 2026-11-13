namespace KasseAPI_Final.Models;

/// <summary>
/// Month grid for the admin daily-closing calendar (Vienna business days).
/// </summary>
public sealed class DailyClosingCalendarDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    public Guid? CashRegisterId { get; set; }

    public IReadOnlyList<DailyClosingDayDto> Days { get; set; } = [];
}

/// <summary>
/// One Vienna calendar day in <see cref="DailyClosingCalendarDto"/>.
/// <see cref="ClosingType"/> here is the day kind (<c>normal</c>/<c>empty</c>), not Daily/Monthly/Yearly.
/// </summary>
public sealed class DailyClosingDayDto
{
    /// <summary>Vienna calendar date (JSON <c>yyyy-MM-dd</c>).</summary>
    public DateOnly Date { get; set; }

    public bool IsClosed { get; set; }

    /// <summary><c>normal</c>, <c>empty</c>, or null when the day is not closed.</summary>
    public string? DayKind { get; set; }

    /// <summary>Alias of <see cref="DayKind"/> for calendar consumers (not period type).</summary>
    public string? ClosingType { get; set; }

    public int TransactionCount { get; set; }

    /// <summary>True when a daily closing can still be created (not closed, not a future Vienna day, register exists).</summary>
    public bool CanClose { get; set; }

    public Guid? ClosingId { get; set; }

    public bool IsToday { get; set; }

    public bool IsFuture { get; set; }
}
