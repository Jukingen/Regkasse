using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Folds one combined (action, status, entity type) group into the statistics dictionary
/// previously built from three separate GroupBy queries.
/// </summary>
public static class AuditLogStatisticsAggregation
{
    public readonly record struct Bucket(string Action, AuditLogStatus Status, string EntityType, int Count);

    public static (DateTime? Start, DateTime? End) ApplyDefaultWindow(
        DateTime? startDate,
        DateTime? endDate,
        DateTime utcNow)
    {
        if (startDate.HasValue || endDate.HasValue)
            return (startDate, endDate);

        return (utcNow.AddDays(-30), utcNow);
    }

    public static Dictionary<string, int> FromCombinedGroups(IEnumerable<Bucket> groups)
    {
        var statistics = new Dictionary<string, int>();
        var total = 0;
        foreach (var group in groups)
        {
            Add(statistics, $"Action_{group.Action}", group.Count);
            Add(statistics, $"Status_{group.Status}", group.Count);
            Add(statistics, $"EntityType_{group.EntityType}", group.Count);
            total += group.Count;
        }

        statistics["Total"] = total;
        return statistics;
    }

    private static void Add(Dictionary<string, int> statistics, string key, int count)
    {
        statistics[key] = statistics.GetValueOrDefault(key) + count;
    }
}
