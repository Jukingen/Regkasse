using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

/// <summary>Optional extras for <see cref="Services.ITagesabschlussService.PerformDailyClosingAsync"/>.</summary>
public sealed class DailyClosingPerformOptions
{
    public static DailyClosingPerformOptions Manual { get; } = new();

    public string Trigger { get; init; } = DailyClosingTriggers.Manual;

    public string? CashCountNote { get; init; }

    public int OpenOrdersCount { get; init; }

    public bool OpenOrdersForced { get; init; }

    public decimal? CashCount { get; init; }

    public decimal? CashDifference { get; init; }

    public bool IsAutomatic => DailyClosingTriggers.IsAutomatic(Trigger);
}
