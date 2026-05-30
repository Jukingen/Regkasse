using System.Text.Json.Serialization;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.DTOs;

/// <summary>
/// Structured schedule fields for admin PUT/GET (persisted as UTC cron on per-tenant <see cref="Models.Backup.BackupScheduleConfiguration"/>).
/// </summary>
public sealed class BackupScheduleConfigurationDto
{
    [JsonPropertyName("frequency")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BackupScheduleFrequency Frequency { get; init; } = BackupScheduleFrequency.Daily;

    /// <summary>UTC hour (0–23).</summary>
    [JsonPropertyName("hourUtc")]
    public int HourUtc { get; init; } = 2;

    /// <summary>UTC minute (0–59).</summary>
    [JsonPropertyName("minuteUtc")]
    public int MinuteUtc { get; init; }

    /// <summary>Cron day-of-week (0=Sunday … 6=Saturday) when <see cref="Frequency"/> is Weekly.</summary>
    [JsonPropertyName("dayOfWeek")]
    public int? DayOfWeek { get; init; }

    /// <summary>Day of month (1–31) when <see cref="Frequency"/> is Monthly.</summary>
    [JsonPropertyName("dayOfMonth")]
    public int? DayOfMonth { get; init; }

    /// <summary>Five-field UTC cron when <see cref="Frequency"/> is Custom.</summary>
    [JsonPropertyName("customCron")]
    public string? CustomCron { get; init; }
}
