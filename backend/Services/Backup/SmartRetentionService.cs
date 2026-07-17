using System.Globalization;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Grandfather-father-son retention classifier and keep/delete selector.
/// Windows: 7 daily → 4 weekly → 12 monthly → 7 yearly (then delete).
/// </summary>
public sealed class SmartRetentionService : ISmartRetentionService
{
    /// <summary>Keep all backups in the last N days.</summary>
    public const int DailyRetentionDays = 7;

    /// <summary>After daily window, keep weekly representatives for N weeks.</summary>
    public const int WeeklyRetentionWeeks = 4;

    /// <summary>After weekly window, keep monthly representatives for N months (~30d each).</summary>
    public const int MonthlyRetentionMonths = 12;

    /// <summary>After monthly window, keep yearly representatives for N years (long-term DR archive).</summary>
    public const int YearlyRetentionYears = 7;

    public RetentionPlan CalculateRetentionPlan(DateTime backupDateUtc, DateTime? utcNow = null)
    {
        var now = NormalizeUtc(utcNow ?? DateTime.UtcNow);
        var backup = NormalizeUtc(backupDateUtc);

        // Future timestamps: treat as newest daily (clock skew / clock jump).
        if (backup > now)
            return RetentionPlan.Daily(backup);

        var days = (now.Date - backup.Date).Days;

        if (days <= DailyRetentionDays)
            return RetentionPlan.Daily(backup);

        if (days <= WeeklyRetentionWeeks * 7)
            return RetentionPlan.Weekly(backup);

        if (days <= MonthlyRetentionMonths * 30)
            return RetentionPlan.Monthly(backup);

        // RKSV-aligned long horizon for sparse yearly DR points (not a substitute for audit retention).
        if (days <= YearlyRetentionYears * 365)
            return RetentionPlan.Yearly(backup);

        return RetentionPlan.Delete(backup);
    }

    public Task<RetentionPlan> CalculateRetentionPlanAsync(
        DateTime backupDate,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(CalculateRetentionPlan(backupDate));
    }

    public IReadOnlyList<Guid> SelectRunsToDelete(
        IReadOnlyList<BackupRetentionCandidate> candidates,
        DateTime? utcNow = null)
    {
        if (candidates.Count == 0)
            return Array.Empty<Guid>();

        var now = NormalizeUtc(utcNow ?? DateTime.UtcNow);
        var deletable = new HashSet<Guid>();

        var classified = candidates
            .Select(c => (
                c.Id,
                Date: NormalizeUtc(c.BackupDateUtc),
                Plan: CalculateRetentionPlan(c.BackupDateUtc, now)))
            .ToList();

        foreach (var item in classified.Where(c => c.Plan.ShouldDelete))
            deletable.Add(item.Id);

        // Daily: keep all — no thinning.

        AddNonKeepers(
            deletable,
            classified.Where(c => c.Plan.Tier == RetentionTier.Weekly),
            c => IsoWeekKey(c.Date));

        AddNonKeepers(
            deletable,
            classified.Where(c => c.Plan.Tier == RetentionTier.Monthly),
            c => $"{c.Date.Year:D4}-{c.Date.Month:D2}");

        AddNonKeepers(
            deletable,
            classified.Where(c => c.Plan.Tier == RetentionTier.Yearly),
            c => c.Date.Year.ToString("D4", CultureInfo.InvariantCulture));

        return deletable.ToList();
    }

    private static void AddNonKeepers(
        HashSet<Guid> deletable,
        IEnumerable<(Guid Id, DateTime Date, RetentionPlan Plan)> items,
        Func<(Guid Id, DateTime Date, RetentionPlan Plan), string> periodKey)
    {
        foreach (var group in items.GroupBy(periodKey))
        {
            var ordered = group.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToList();
            // Keep newest in the period; delete older duplicates.
            foreach (var drop in ordered.Skip(1))
                deletable.Add(drop.Id);
        }
    }

    private static string IsoWeekKey(DateTime utcDate)
    {
        var d = utcDate.Date;
        var week = ISOWeek.GetWeekOfYear(d);
        var year = ISOWeek.GetYear(d);
        return $"{year:D4}-W{week:D2}";
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
