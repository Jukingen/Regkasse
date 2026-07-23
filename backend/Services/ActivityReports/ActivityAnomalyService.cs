using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.ActivityReports;

/// <summary>
/// Heuristic anomaly detection from weekly operation-log summaries + unresolved risk scores.
/// </summary>
public sealed class ActivityAnomalyService : IActivityAnomalyService
{
    public const int HighVolumeThreshold = 50;
    public const double DominantShareThreshold = 0.6;
    public const int DominantMinCount = 20;
    public const double HighUndoRateThreshold = 0.3;
    public const int HighUndoMinCount = 5;

    private readonly AppDbContext _db;

    public ActivityAnomalyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ActivityAnomalyDto>> DetectAnomaliesAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<ActivitySummaryDto> activities,
        CancellationToken cancellationToken = default)
    {
        var anomalies = new List<ActivityAnomalyDto>();
        var total = activities.Sum(a => a.Count);

        foreach (var row in activities)
        {
            if (row.Count >= HighVolumeThreshold)
            {
                anomalies.Add(new ActivityAnomalyDto
                {
                    Code = "HIGH_VOLUME",
                    OperationType = row.OperationType,
                    Severity = row.Count >= HighVolumeThreshold * 2 ? "High" : "Medium",
                    Description =
                        $"Operation '{row.OperationType}' occurred {row.Count} times in the last 7 days (threshold {HighVolumeThreshold}).",
                    Recommendation =
                        "Review whether this volume is expected for the tenant workload, or investigate automation / bulk abuse.",
                });
            }

            if (total >= DominantMinCount
                && row.Count >= DominantMinCount
                && row.Count >= total * DominantShareThreshold)
            {
                anomalies.Add(new ActivityAnomalyDto
                {
                    Code = "DOMINANT_OPERATION",
                    OperationType = row.OperationType,
                    Severity = "Medium",
                    Description =
                        $"Operation '{row.OperationType}' accounts for {row.Count} of {total} activities ({(100.0 * row.Count / total):0}%).",
                    Recommendation =
                        "Confirm this concentration is intentional; otherwise audit recent actors for that operation type.",
                });
            }

            if (row.Count >= DominantMinCount && row.Users == 1)
            {
                anomalies.Add(new ActivityAnomalyDto
                {
                    Code = "SINGLE_USER_BURST",
                    OperationType = row.OperationType,
                    Severity = "Medium",
                    Description =
                        $"All {row.Count} '{row.OperationType}' operations were performed by a single user.",
                    Recommendation =
                        "Verify the user account and consider reviewing their recent sessions and IP history.",
                });
            }
        }

        var logs = _db.OperationLogs.AsNoTracking().IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId && o.CreatedAt >= fromUtc && o.CreatedAt <= toUtc);

        var totalOps = await logs.CountAsync(cancellationToken).ConfigureAwait(false);
        var undoneOps = await logs.CountAsync(o => o.IsUndone, cancellationToken).ConfigureAwait(false);
        if (totalOps >= HighUndoMinCount && undoneOps >= HighUndoMinCount
            && undoneOps >= totalOps * HighUndoRateThreshold)
        {
            anomalies.Add(new ActivityAnomalyDto
            {
                Code = "HIGH_UNDO_RATE",
                Severity = "High",
                Description =
                    $"{undoneOps} of {totalOps} operations were undone ({(100.0 * undoneOps / totalOps):0}%).",
                Recommendation =
                    "High undo rates may indicate operator mistakes, training gaps, or contested changes — review undo reasons.",
            });
        }

        var unresolvedRisks = await _db.RiskScores.AsNoTracking().IgnoreQueryFilters()
            .Where(r =>
                r.TenantId == tenantId
                && !r.IsResolved
                && r.CreatedAt >= fromUtc
                && (r.RiskLevel == RiskLevels.High || r.RiskLevel == RiskLevels.Critical))
            .OrderByDescending(r => r.Score)
            .Take(10)
            .Select(r => new { r.ActionType, r.RiskLevel, r.Score, r.Reason })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var risk in unresolvedRisks)
        {
            anomalies.Add(new ActivityAnomalyDto
            {
                Code = "UNRESOLVED_RISK_SCORE",
                OperationType = risk.ActionType,
                Severity = risk.RiskLevel,
                Description =
                    $"Unresolved {risk.RiskLevel} risk score ({risk.Score}): {risk.Reason}",
                Recommendation =
                    "Open the risk dashboard, investigate the actor, and resolve or escalate the finding.",
            });
        }

        return anomalies;
    }
}
