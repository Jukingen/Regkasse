namespace KasseAPI_Final.Models;

/// <summary>Dashboard widget snapshot for daily-closing status (Vienna business days).</summary>
public sealed class DailyClosingDashboardSummaryDto
{
    public Guid? CashRegisterId { get; set; }

    public DailyClosingDaySummaryDto Today { get; set; } = new();

    public DailyClosingWeekSummaryDto Week { get; set; } = new();

    public DailyClosingLastClosingDto? LastClosing { get; set; }

    /// <summary>True when today is still open and has paid fiscal transactions.</summary>
    public bool RequiresAttention { get; set; }
}

public sealed class DailyClosingDaySummaryDto
{
    public DateOnly Date { get; set; }

    public bool IsClosed { get; set; }

    public string? DayKind { get; set; }

    /// <summary>Alias of <see cref="DayKind"/> (normal/empty), not Daily/Monthly/Yearly.</summary>
    public string? ClosingType { get; set; }

    public int TransactionCount { get; set; }

    public bool CanClose { get; set; }

    public Guid? ClosingId { get; set; }
}

public sealed class DailyClosingWeekSummaryDto
{
    public DateOnly Start { get; set; }

    public DateOnly End { get; set; }

    public int TotalDays { get; set; }

    public int ClosedDays { get; set; }

    public int EmptyDays { get; set; }

    public int OpenDays { get; set; }

    public int NoTransactionDays { get; set; }

    public int FutureDays { get; set; }
}

public sealed class DailyClosingLastClosingDto
{
    public DateOnly Date { get; set; }

    /// <summary>Real UTC creation instant (<see cref="DailyClosing.CreatedAt"/>), never backdated.</summary>
    public DateTime ClosedAt { get; set; }

    public string? DayKind { get; set; }

    public Guid? ClosingId { get; set; }

    public int TransactionCount { get; set; }
}
