namespace KasseAPI_Final.Services.Analytics;

/// <summary>UTC-day signature counts for the Super Admin TSE usage chart (diagnostic only).</summary>
public sealed record TseDailyUsageDto(DateTime Date, int Signatures);

/// <summary>
/// Fleet TSE usage snapshot. Diagnostic Super Admin KPI — not DEP / Finanzamt evidence.
/// </summary>
public sealed record TseAnalyticsDto(
    int TotalRegisters,
    int ActiveRegisters,
    int TseEnabled,
    int TseDisabled,
    int SignaturesToday,
    int SignaturesThisMonth,
    int FailedSignatures,
    decimal AverageSignaturesPerRegister,
    IReadOnlyList<TseDailyUsageDto> DailyUsage,
    bool DiagnosticOnly = true);

public interface ITseUsageAnalyticsService
{
    Task<TseAnalyticsDto> GetTseAnalyticsAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);
}
