using System.Text.RegularExpressions;
using Cronos;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Maps structured schedule DTOs to/from five-field UTC cron (CronFormat.Standard).</summary>
public static partial class BackupScheduleCronCodec
{
    private const int MinHour = 0;
    private const int MaxHour = 23;
    private const int MinMinute = 0;
    private const int MaxMinute = 59;
    private const int MinDayOfMonth = 1;
    private const int MaxDayOfMonth = 31;
    private const int MinDayOfWeek = 0;
    private const int MaxDayOfWeek = 6;

    public static string BuildCron(BackupScheduleConfigurationDto schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ValidateSchedule(schedule);

        if (schedule.Frequency == BackupScheduleFrequency.Custom)
            return NormalizeCron(schedule.CustomCron!);

        return schedule.Frequency switch
        {
            BackupScheduleFrequency.Daily =>
                $"{schedule.MinuteUtc} {schedule.HourUtc} * * *",
            BackupScheduleFrequency.Weekly =>
                $"{schedule.MinuteUtc} {schedule.HourUtc} * * {schedule.DayOfWeek!.Value}",
            BackupScheduleFrequency.Monthly =>
                $"{schedule.MinuteUtc} {schedule.HourUtc} {schedule.DayOfMonth!.Value} * *",
            _ => throw new ArgumentOutOfRangeException(nameof(schedule.Frequency)),
        };
    }

    public static bool TryParseCron(string? cron, out BackupScheduleConfigurationDto? configuration)
    {
        configuration = null;
        if (string.IsNullOrWhiteSpace(cron))
            return false;

        var n = NormalizeCron(cron);
        if (!CronExpression.TryParse(n, CronFormat.Standard, out _))
            return false;

        var m = ParsedCronRegex().Match(n);
        if (!m.Success)
        {
            configuration = new BackupScheduleConfigurationDto
            {
                Frequency = BackupScheduleFrequency.Custom,
                CustomCron = n,
                HourUtc = 2,
                MinuteUtc = 0,
            };
            return true;
        }

        var minute = int.Parse(m.Groups["minute"].Value);
        var hour = int.Parse(m.Groups["hour"].Value);
        var dom = m.Groups["dom"].Value;
        var month = m.Groups["month"].Value;
        var dow = m.Groups["dow"].Value;

        if (dom == "*" && month == "*" && dow == "*")
        {
            configuration = new BackupScheduleConfigurationDto
            {
                Frequency = BackupScheduleFrequency.Daily,
                HourUtc = hour,
                MinuteUtc = minute,
            };
            return true;
        }

        if (dom == "*" && month == "*" && dow != "*")
        {
            configuration = new BackupScheduleConfigurationDto
            {
                Frequency = BackupScheduleFrequency.Weekly,
                HourUtc = hour,
                MinuteUtc = minute,
                DayOfWeek = int.Parse(dow),
            };
            return true;
        }

        if (dom != "*" && month == "*" && dow == "*")
        {
            configuration = new BackupScheduleConfigurationDto
            {
                Frequency = BackupScheduleFrequency.Monthly,
                HourUtc = hour,
                MinuteUtc = minute,
                DayOfMonth = int.Parse(dom),
            };
            return true;
        }

        configuration = new BackupScheduleConfigurationDto
        {
            Frequency = BackupScheduleFrequency.Custom,
            CustomCron = n,
            HourUtc = hour,
            MinuteUtc = minute,
        };
        return true;
    }

    public static void ValidateSchedule(BackupScheduleConfigurationDto schedule)
    {
        if (schedule.HourUtc < MinHour || schedule.HourUtc > MaxHour)
            throw new ArgumentOutOfRangeException(nameof(schedule.HourUtc));
        if (schedule.MinuteUtc < MinMinute || schedule.MinuteUtc > MaxMinute)
            throw new ArgumentOutOfRangeException(nameof(schedule.MinuteUtc));

        switch (schedule.Frequency)
        {
            case BackupScheduleFrequency.Daily:
                return;
            case BackupScheduleFrequency.Weekly:
                if (schedule.DayOfWeek is < MinDayOfWeek or > MaxDayOfWeek)
                    throw new ArgumentOutOfRangeException(nameof(schedule.DayOfWeek));
                return;
            case BackupScheduleFrequency.Monthly:
                if (schedule.DayOfMonth is < MinDayOfMonth or > MaxDayOfMonth)
                    throw new ArgumentOutOfRangeException(nameof(schedule.DayOfMonth));
                return;
            case BackupScheduleFrequency.Custom:
                var cron = NormalizeCron(schedule.CustomCron ?? string.Empty);
                if (string.IsNullOrEmpty(cron) || !CronExpression.TryParse(cron, CronFormat.Standard, out _))
                    throw new ArgumentException("INVALID_CRON_EXPRESSION", nameof(schedule.CustomCron));
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(schedule.Frequency));
        }
    }

    private static string NormalizeCron(string cron) =>
        cron.Trim().Replace("  ", " ", StringComparison.Ordinal);

    // minute hour dom month dow — numeric literals only (presets UI)
    [GeneratedRegex(
        @"^(?<minute>\d{1,2})\s+(?<hour>\d{1,2})\s+(?<dom>\*|\d{1,2})\s+(?<month>\*|\d{1,2})\s+(?<dow>\*|\d{1,2})$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ParsedCronRegex();
}
