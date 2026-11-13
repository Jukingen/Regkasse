using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Models;

/// <summary>Outcome of <see cref="Services.IDailyClosingService.CreateDailyClosingAsync"/>.</summary>
public sealed class DailyClosingResult
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public DailyClosingDto? Closing { get; init; }

    /// <summary>True when the created closing covers a past Vienna business day (nachträglich).</summary>
    public bool IsBackdated { get; init; }

    /// <summary>True when the created closing has no fiscal invoices for the business day.</summary>
    public bool IsEmpty { get; init; }

    /// <summary>Persisted day kind (<c>normal</c> / <c>empty</c>).</summary>
    public string DayKind { get; init; } = DailyClosingDayKinds.Normal;

    public PaymentBreakdown PaymentBreakdown { get; init; } = new();

    public DailyClosingTaxBreakdownDto TaxBreakdown { get; init; } = new();
}
