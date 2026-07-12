using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Models;

/// <summary>Outcome of <see cref="Services.IDailyClosingService.CreateDailyClosingAsync"/>.</summary>
public sealed class DailyClosingResult
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public DailyClosingDto? Closing { get; init; }

    public PaymentBreakdown PaymentBreakdown { get; init; } = new();

    public DailyClosingTaxBreakdownDto TaxBreakdown { get; init; } = new();
}
