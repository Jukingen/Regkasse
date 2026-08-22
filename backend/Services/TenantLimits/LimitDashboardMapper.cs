using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;

namespace KasseAPI_Final.Services.Limits;

/// <summary>Maps live usage snapshots to dashboard status rows (80% warning / 100% critical).</summary>
internal static class LimitDashboardMapper
{
    public static IReadOnlyList<LimitStatusDto> FromUsage(
        TenantLimitUsageDto usage,
        string? tenantName,
        decimal currentMaxTransactionAmount = 0m,
        IReadOnlyDictionary<string, int>? changeCounts = null,
        string? tenantSlug = null)
    {
        ArgumentNullException.ThrowIfNull(usage);
        var caps = usage.Limits;
        int Change(string key) => changeCounts != null && changeCounts.TryGetValue(key, out var n) ? n : 0;
        return
        [
            Row(usage.TenantId, tenantName, tenantSlug, TenantLimitKeys.MaxActiveRegistersPerUser,
                caps.MaxActiveRegistersPerUser, usage.CurrentMaxAssignedRegistersPerUser,
                Change(TenantLimitKeys.MaxActiveRegistersPerUser)),
            Row(usage.TenantId, tenantName, tenantSlug, TenantLimitKeys.MaxProductsPerTenant,
                caps.MaxProductsPerTenant, usage.CurrentProducts,
                Change(TenantLimitKeys.MaxProductsPerTenant)),
            Row(usage.TenantId, tenantName, tenantSlug, TenantLimitKeys.MaxUsersPerTenant,
                caps.MaxUsersPerTenant, usage.CurrentUsers,
                Change(TenantLimitKeys.MaxUsersPerTenant)),
            Row(usage.TenantId, tenantName, tenantSlug, TenantLimitKeys.DailyMaxTransactions,
                caps.DailyMaxTransactions, usage.CurrentDailyTransactions,
                Change(TenantLimitKeys.DailyMaxTransactions)),
            Row(usage.TenantId, tenantName, tenantSlug, TenantLimitKeys.MaxTransactionAmount,
                caps.MaxTransactionAmount, currentMaxTransactionAmount,
                Change(TenantLimitKeys.MaxTransactionAmount)),
            Row(usage.TenantId, tenantName, tenantSlug, TenantLimitKeys.DailyMaxRevenue,
                caps.DailyMaxRevenue, usage.CurrentDailyRevenue,
                Change(TenantLimitKeys.DailyMaxRevenue)),
            Row(usage.TenantId, tenantName, tenantSlug, TenantLimitKeys.MaxBackupsPerTenant,
                caps.MaxBackupsPerTenant, usage.CurrentBackups,
                Change(TenantLimitKeys.MaxBackupsPerTenant)),
            Row(usage.TenantId, tenantName, tenantSlug, TenantLimitKeys.MaxBackupSizeMb,
                caps.MaxBackupSizeMb, usage.CurrentBackupSizeMb,
                Change(TenantLimitKeys.MaxBackupSizeMb)),
            Row(usage.TenantId, tenantName, tenantSlug, TenantLimitKeys.MaxOfflineTransactions,
                caps.MaxOfflineTransactions, usage.CurrentOfflineTransactions,
                Change(TenantLimitKeys.MaxOfflineTransactions)),
        ];
    }

    public static LimitStatusDto Row(
        Guid tenantId,
        string? tenantName,
        string? tenantSlug,
        string limitKey,
        decimal limit,
        decimal current,
        int changeCount = 0)
    {
        var catalog = Describe(limitKey);
        return new LimitStatusDto
        {
            TenantId = tenantId,
            TenantName = tenantName,
            TenantSlug = tenantSlug,
            Key = limitKey,
            DisplayName = catalog.DisplayName,
            Description = catalog.Description,
            Current = ToInt(current),
            Limit = ToInt(limit),
            Percentage = (double)ComputePercent(limit, current),
            Status = ClassifyHealth(limit, current),
            Trend = ClassifyTrend(changeCount),
            ChangeCount = changeCount,
            ChangeUnit = catalog.ChangeUnit,
        };
    }

    public static string ClassifyHealth(decimal limit, decimal current)
    {
        if (limit <= 0)
            return LimitUsageStatuses.Healthy;
        if (current >= limit)
            return LimitUsageStatuses.Critical;
        if (current >= limit * LimitUsageStatuses.ApproachingRatio)
            return LimitUsageStatuses.Warning;
        return LimitUsageStatuses.Healthy;
    }

    /// <summary>User-row status: approaching (≥80%), full (=100%), exceeded (&gt;100%).</summary>
    public static string? ClassifyUser(decimal limit, decimal current)
    {
        if (limit <= 0)
            return null;
        if (current > limit)
            return LimitUsageStatuses.Exceeded;
        if (current >= limit)
            return LimitUsageStatuses.Full;
        if (current >= limit * LimitUsageStatuses.ApproachingRatio)
            return LimitUsageStatuses.Approaching;
        return null;
    }

    public static string ClassifyTrend(int changeCount)
    {
        if (changeCount > 0)
            return LimitUsageStatuses.Increasing;
        if (changeCount < 0)
            return LimitUsageStatuses.Decreasing;
        return LimitUsageStatuses.Stable;
    }

    public static decimal ComputePercent(decimal limit, decimal current)
    {
        if (limit <= 0)
            return 0m;
        return Math.Round(current / limit * 100m, 2, MidpointRounding.AwayFromZero);
    }

    public static int ToInt(decimal value) =>
        (int)Math.Round(value, MidpointRounding.AwayFromZero);

    public static bool IsDailyKey(string limitKey) =>
        string.Equals(limitKey, TenantLimitKeys.DailyMaxTransactions, StringComparison.Ordinal)
        || string.Equals(limitKey, TenantLimitKeys.DailyMaxRevenue, StringComparison.Ordinal)
        || string.Equals(limitKey, TenantLimitKeys.MaxTransactionAmount, StringComparison.Ordinal);

    public static string DedupKey(ActivityEventType type, string limitKey)
    {
        var prefix = type == ActivityEventType.LimitExceeded
            ? "limit_exceeded"
            : "limit_approaching";
        if (IsDailyKey(limitKey))
            return $"{prefix}_{limitKey}_{DateTime.UtcNow:yyyyMMdd}";
        return $"{prefix}_{limitKey}";
    }

    public static ActivityEventPublishRequest ToPublishRequest(
        Guid tenantId,
        ActivityEventType type,
        string limitKey,
        decimal limit,
        decimal current)
    {
        var percent = ComputePercent(limit, current);
        var approaching = type == ActivityEventType.LimitApproaching;
        var title = approaching ? "Limit approaching" : "Limit exceeded";
        var description = approaching
            ? $"Limit {limitKey} is at {percent:0.##}% ({current:0.##}/{limit:0.##})."
            : $"Limit {limitKey} exceeded ({current:0.##}/{limit:0.##}).";

        return new ActivityEventPublishRequest(
            tenantId,
            type,
            title,
            Description: description,
            DedupKey: DedupKey(type, limitKey),
            EntityType: "tenant_limit",
            EntityId: limitKey,
            Metadata: new Dictionary<string, object>
            {
                ["LimitKey"] = limitKey,
                ["Limit"] = limit,
                ["Current"] = current,
                ["UsagePercent"] = percent,
                ["Message"] = description,
            });
    }

    public static string ActivityStatus(ActivityEventType type) =>
        type == ActivityEventType.LimitExceeded
            ? LimitUsageStatuses.Critical
            : LimitUsageStatuses.Warning;

    public static (string DisplayName, string Description, string ChangeUnit) Describe(string limitKey) =>
        limitKey switch
        {
            TenantLimitKeys.MaxProductsPerTenant => (
                "Max. products per tenant",
                "Active catalog products counted toward the mandant cap.",
                "products"),
            TenantLimitKeys.MaxUsersPerTenant => (
                "Max. users per tenant",
                "Active tenant memberships counted toward the mandant cap.",
                "users"),
            TenantLimitKeys.DailyMaxTransactions => (
                "Max. transactions per day",
                "Fiscal POS sales completed today (UTC).",
                "transactions"),
            TenantLimitKeys.MaxTransactionAmount => (
                "Max. amount per transaction",
                "Largest POS ticket today compared with the per-sale cap (EUR).",
                "EUR"),
            TenantLimitKeys.DailyMaxRevenue => (
                "Max. daily revenue",
                "Sum of POS sales today (UTC) compared with the daily revenue cap (EUR).",
                "EUR"),
            TenantLimitKeys.MaxActiveRegistersPerUser => (
                "Max. registers per cashier",
                "Peak count of non-decommissioned cash registers assigned to a single user.",
                "registers"),
            TenantLimitKeys.MaxBackupsPerTenant => (
                "Max. backups per tenant",
                "Succeeded tenant backup runs counted toward the mandant cap.",
                "backups"),
            TenantLimitKeys.MaxBackupSizeMb => (
                "Max. backup size",
                "Cumulative succeeded tenant dump size in megabytes.",
                "MB"),
            TenantLimitKeys.MaxOfflineTransactions => (
                "Max. offline transactions",
                "Pending TSE offline intents queued tenant-wide.",
                "intents"),
            _ => (limitKey, string.Empty, "units"),
        };

    public static string RecommendedAction(string limitKey, string userStatus)
    {
        if (string.Equals(limitKey, TenantLimitKeys.MaxActiveRegistersPerUser, StringComparison.Ordinal))
        {
            return userStatus switch
            {
                LimitUsageStatuses.Exceeded =>
                    "This user exceeds the assignment cap. Unassign unused registers immediately.",
                LimitUsageStatuses.Full =>
                    "This user is at the assignment cap. Unassign a register before assigning another.",
                _ =>
                    "Review register assignments and unassign unused registers before this user reaches the cap.",
            };
        }

        return userStatus switch
        {
            LimitUsageStatuses.Exceeded => "Reduce usage or raise the limit with Super Admin.",
            LimitUsageStatuses.Full => "The cap is reached. Free capacity before adding more.",
            _ => "Usage is approaching the cap. Plan to free capacity or raise the limit.",
        };
    }

    public static int DeltaVsAverage(int today, int previousTotal, int previousDays)
    {
        if (previousDays <= 0)
            return 0;
        var avg = (int)Math.Round(previousTotal / (decimal)previousDays, MidpointRounding.AwayFromZero);
        return today - avg;
    }
}
