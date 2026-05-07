namespace KasseAPI_Final.DTOs;

/// <summary>RKSV Monatsbeleg reminder status for a cash register (Vienna calendar month).</summary>
public sealed class MonatsbelegStatusDto
{
    public required bool IsRequired { get; init; }

    /// <summary>Whole calendar days from Vienna “today” until the last day of the current Vienna month (0 on month end day).</summary>
    public required int DaysUntilDeadline { get; init; }

    /// <summary>UTC instant of the last Monatsbeleg (or December Jahresbeleg that closes the month) for this register, when known.</summary>
    public DateTime? LastMonatsbelegDate { get; init; }

    /// <summary>One of: green, yellow, red.</summary>
    public required string WarningLevel { get; init; }
}
