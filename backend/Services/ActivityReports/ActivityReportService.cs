using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.ActivityReports;

public sealed class ActivityReportService : IActivityReportService
{
    public const int WeeklyLookbackDays = 7;

    private readonly AppDbContext _db;
    private readonly IActivityAnomalyService _anomalyService;
    private readonly ILogger<ActivityReportService> _logger;

    public ActivityReportService(
        AppDbContext db,
        IActivityAnomalyService anomalyService,
        ILogger<ActivityReportService> logger)
    {
        _db = db;
        _anomalyService = anomalyService;
        _logger = logger;
    }

    public async Task<ActivityReportDto?> GenerateWeeklyReportAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantExists = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!tenantExists)
            return null;

        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddDays(-WeeklyLookbackDays);

        var baseQuery = _db.OperationLogs.AsNoTracking().IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId && o.CreatedAt >= fromUtc && o.CreatedAt <= toUtc);

        var activities = await baseQuery
            .GroupBy(o => o.OperationType)
            .Select(g => new ActivitySummaryDto
            {
                OperationType = g.Key,
                Count = g.Count(),
                Users = g.Select(o => o.UserId).Distinct().Count(),
                FirstOccurrence = g.Min(o => o.CreatedAt),
                LastOccurrence = g.Max(o => o.CreatedAt),
            })
            .OrderByDescending(a => a.Count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalActivities = activities.Sum(a => a.Count);
        var uniqueUsers = await baseQuery
            .Select(o => o.UserId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var anomalies = await _anomalyService
            .DetectAnomaliesAsync(tenantId, fromUtc, toUtc, activities, cancellationToken)
            .ConfigureAwait(false);

        var recommendations = GenerateRecommendations(activities, anomalies, totalActivities, uniqueUsers);

        _logger.LogInformation(
            "Weekly activity report generated for tenant {TenantId}: {Total} ops, {Users} users, {Anomalies} anomalies",
            tenantId,
            totalActivities,
            uniqueUsers,
            anomalies.Count);

        return new ActivityReportDto
        {
            TenantId = tenantId,
            Period = new ActivityReportDateRangeDto { FromUtc = fromUtc, ToUtc = toUtc },
            TotalActivities = totalActivities,
            UniqueUsers = uniqueUsers,
            ActivitySummary = activities,
            Anomalies = anomalies,
            Recommendations = recommendations,
        };
    }

    internal static IReadOnlyList<string> GenerateRecommendations(
        IReadOnlyList<ActivitySummaryDto> activities,
        IReadOnlyList<ActivityAnomalyDto> anomalies,
        int totalActivities,
        int uniqueUsers)
    {
        var list = new List<string>();

        if (totalActivities == 0)
        {
            list.Add("No operation-log activity in the last 7 days. Confirm the tenant is still active or that logging is enabled for admin mutations.");
            return list;
        }

        if (uniqueUsers == 1 && totalActivities >= 10)
            list.Add("Only one user produced all logged activity — consider reviewing access distribution and backup operators.");

        if (anomalies.Count == 0)
            list.Add("No anomalies detected for this period. Continue routine monitoring.");
        else
            list.Add($"Address {anomalies.Count} detected anomal{(anomalies.Count == 1 ? "y" : "ies")} before the next weekly review.");

        if (activities.Any(a => a.OperationType == OperationTypes.CreatePayment && a.Count > 0))
            list.Add("Payment operations appear in the journal — remember fiscal payments remain non-undoable; use storno/refund flows when needed.");

        foreach (var anomaly in anomalies.Where(a => !string.IsNullOrWhiteSpace(a.Recommendation)).Take(5))
        {
            if (!list.Contains(anomaly.Recommendation))
                list.Add(anomaly.Recommendation);
        }

        return list;
    }
}
