using System.Text.Json;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Trial;

public sealed class ConvertToPaidRequest
{
    public Guid LicenseSaleId { get; set; }

    /// <summary>When true (default), append remaining trial days onto the license end date.</summary>
    public bool? AddRemainingTrialDays { get; set; } = true;

    public string? Notes { get; set; }
}

public sealed record TrialConversionResult(
    bool Success,
    Guid TenantId,
    Guid LicenseSaleId,
    DateTime LicenseValidUntilUtc,
    DateTime ConversionDateUtc,
    int RemainingTrialDaysAdded,
    string? LicensePlan,
    string? LicenseKey,
    string? Error = null,
    string? Message = null);

public sealed record TrialAnalyticsDto(
    int TrialsCreatedLast30Days,
    int ActiveTrials,
    int ExpiredTrials,
    int ConvertedTrials,
    int DeletedTrials,
    double ConversionRatePercent,
    double? AverageDaysToConvert,
    string? MostCommonLicensePlan,
    IReadOnlyList<TrialDurationConversionBucketDto> ConversionByTrialDuration,
    IReadOnlyList<TrialPlanConversionBucketDto> ConversionByPlan,
    IReadOnlyList<TrialMonthlyTrendDto> MonthlyTrend);

public sealed record TrialDurationConversionBucketDto(
    int TrialDurationDays,
    int ConvertedCount,
    int TotalStarted);

public sealed record TrialPlanConversionBucketDto(
    string LicensePlan,
    int ConvertedCount);

public sealed record TrialMonthlyTrendDto(
    string YearMonth,
    int TrialsStarted,
    int Converted);

public interface ITrialConversionService
{
    Task<(TrialConversionResult? Result, string? Error)> ConvertToPaidAsync(
        Guid tenantId,
        Guid licenseSaleId,
        bool addRemainingTrialDays = true,
        string? notes = null,
        string? actorUserId = null,
        string? actorRole = null,
        CancellationToken cancellationToken = default);
}
