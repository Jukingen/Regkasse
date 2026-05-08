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
