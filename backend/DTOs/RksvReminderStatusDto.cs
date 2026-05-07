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
}

public sealed class RksvReminderJahresbelegDto
{
    public required bool IsRequired { get; init; }

    /// <summary>Typically days until 31.12. of the obligation year in December; null if not in window or satisfied.</summary>
    public int? DaysUntilDeadline { get; init; }

    /// <summary>ok | upcoming | overdue</summary>
    public required string Status { get; init; }
}
