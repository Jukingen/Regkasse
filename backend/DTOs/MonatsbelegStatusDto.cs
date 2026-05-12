namespace KasseAPI_Final.DTOs;

/// <summary>Detailed RKSV Monatsbeleg compliance status for a cash register.</summary>
public sealed class MonatsbelegStatusDto
{
    /// <summary>Last completed Monat in yyyy-MM format; null when none exists.</summary>
    public string? LastCompletedMonth { get; init; }

    /// <summary>Earliest missing Monat in yyyy-MM format; null when no month is missing.</summary>
    public string? NextRequiredMonth { get; init; }

    /// <summary>Missing months that do not have a Monatsbeleg (or applicable December Jahresbeleg substitute).</summary>
    public required IReadOnlyList<MissingMonth> MissingMonths { get; init; }

    /// <summary>True when at least one month is missing (including grace-period months).</summary>
    public required bool RequiresAttention { get; init; }

    /// <summary>Total number of missing months.</summary>
    public required int TotalMissingCount { get; init; }

    /// <summary>POS banner: same as <see cref="RequiresAttention"/>.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Days until the legal deadline of the earliest missing month (0 when none).</summary>
    public int DaysUntilDeadline { get; init; }

    /// <summary>ISO-8601 UTC timestamp of the latest Monatsbeleg row for this register, if any.</summary>
    public string? LastMonatsbelegDate { get; init; }

    /// <summary>POS strip: <c>none</c>, <c>yellow</c> (reminder after the 7th Vienna calendar day when attention is needed), or <c>red</c> (overdue).</summary>
    public string WarningLevel { get; init; } = "none";

    /// <summary>True when a Monatsbeleg (or December Jahresbeleg substitute per policy) exists for the current Vienna calendar month.</summary>
    public bool CurrentMonthExists { get; init; }

    /// <summary>True when the immediately preceding Vienna calendar month is satisfied (same policy as current month).</summary>
    public bool LastMonthExists { get; init; }

    /// <summary>True when the current Vienna month is in scope, has no Monatsbeleg yet, and the calendar day is after the 7th (operator grace).</summary>
    public bool CurrentMonthOverdue { get; init; }

    /// <summary>True when the previous Vienna month is in the compliance window and has no Monatsbeleg.</summary>
    public bool LastMonthMissing { get; init; }

    /// <summary>Optional German operator copy for POS/dashboard banners (null when no dedicated message).</summary>
    public string? WarningMessage { get; init; }
}

public sealed class MissingMonth
{
    public required int Year { get; init; }
    public required int Month { get; init; }

    /// <summary>True when current Vienna date is after the legal deadline (end of following month).</summary>
    public required bool IsOverdue { get; init; }

    /// <summary>Legal completion deadline (Vienna calendar date): end of following month.</summary>
    public required DateOnly Deadline { get; init; }
}
