using Cronos;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>UTC cron projections and due checks for <see cref="BackupScheduleConfiguration"/>.</summary>
public static class BackupScheduleProjectionHelper
{
    public static DateTime? ComputeNextRunUtc(string scheduleCron, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(scheduleCron)
            || !CronExpression.TryParse(scheduleCron.Trim(), CronFormat.Standard, out var expr))
            return null;

        return expr.GetNextOccurrence(utcNow, TimeZoneInfo.Utc, inclusive: false);
    }

    public static void RefreshNextRunAt(BackupScheduleConfiguration row, DateTime utcNow)
    {
        if (!row.Enabled)
        {
            row.NextRunAt = null;
            return;
        }

        row.NextRunAt = ComputeNextRunUtc(row.ScheduleCron, utcNow);
    }

    /// <summary>
    /// True when the next cron occurrence after <see cref="BackupScheduleConfiguration.LastRunAt"/> (or epoch) is at or before <paramref name="utcNow"/>.
    /// </summary>
    public static bool IsScheduleDue(BackupScheduleConfiguration row, DateTime utcNow)
    {
        if (!row.Enabled || string.IsNullOrWhiteSpace(row.ScheduleCron))
            return false;

        if (!CronExpression.TryParse(row.ScheduleCron.Trim(), CronFormat.Standard, out var expr))
            return false;

        var anchor = row.LastRunAt ?? utcNow.AddYears(-10);
        var nextFire = expr.GetNextOccurrence(anchor, TimeZoneInfo.Utc, inclusive: false);
        return nextFire != null && nextFire.Value <= utcNow;
    }
}
