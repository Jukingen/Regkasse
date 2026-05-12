namespace KasseAPI_Final.DTOs;

/// <summary>Unified RKSV special-receipt reminder payload for a cash register.</summary>
public sealed class RksvReminderStatusDto
{
    public required RksvReminderStartbelegDto Startbeleg { get; init; }

    public required RksvReminderMonatsbelegDto Monatsbeleg { get; init; }

    public required RksvReminderJahresbelegDto Jahresbeleg { get; init; }
}

public sealed class RksvReminderStartbelegDto
{
    public required bool IsRequired { get; init; }

    /// <summary>missing = cash register StartbelegCreatedAt unset; present = timestamp recorded after Startbeleg issuance.</summary>
    public required string Status { get; init; }
}

public sealed class RksvReminderMonatsbelegDto
{
    public required bool IsRequired { get; init; }

    /// <summary>Days until end of current Vienna month when relevant; null when not applicable.</summary>
    public int? DaysUntilDeadline { get; init; }

    /// <summary>ok | upcoming | overdue</summary>
    public required string Status { get; init; }

    /// <summary>Monatsbeleg present for current Vienna month (policy includes December Jahresbeleg substitute when configured).</summary>
    public bool CurrentMonthExists { get; init; }

    /// <summary>Monatsbeleg present for the immediately preceding Vienna calendar month.</summary>
    public bool LastMonthExists { get; init; }

    /// <summary>No current-month Monatsbeleg and Vienna calendar day &gt; 7 (grace elapsed).</summary>
    public bool CurrentMonthOverdue { get; init; }

    /// <summary>Previous Vienna month has no Monatsbeleg (within obligation window).</summary>
    public bool LastMonthMissing { get; init; }

    /// <summary>German operator hint; null when nothing specific.</summary>
    public string? WarningMessageDe { get; init; }
}

public sealed class RksvReminderJahresbelegDto
{
    public required bool IsRequired { get; init; }

    /// <summary>Typically days until 31.12. of the obligation year in December; null if not in window or satisfied.</summary>
    public int? DaysUntilDeadline { get; init; }

    /// <summary>ok | upcoming | overdue</summary>
    public required string Status { get; init; }
}
