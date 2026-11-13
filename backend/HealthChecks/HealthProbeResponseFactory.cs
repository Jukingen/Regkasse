using KasseAPI_Final.DTOs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KasseAPI_Final.HealthChecks;

/// <summary>Builds <see cref="HealthProbeResponseDto"/> from a <see cref="HealthReport"/>.</summary>
public static class HealthProbeResponseFactory
{
    public static HealthProbeResponseDto FromReport(HealthReport report, string? releaseStage = null) =>
        new()
        {
            Status = report.Status.ToString(),
            TotalDurationMs = report.TotalDuration.TotalMilliseconds,
            CheckedAtUtc = DateTime.UtcNow,
            ReleaseStage = releaseStage,
            RedisStatus = ResolveRedisStatus(report),
            Entries = report.Entries.ToDictionary(
                e => e.Key,
                e => new HealthProbeEntryDto
                {
                    Status = e.Value.Status.ToString(),
                    Description = e.Value.Description,
                    DurationMs = e.Value.Duration.TotalMilliseconds,
                    Data = e.Value.Data,
                },
                StringComparer.Ordinal),
        };

    /// <summary>
    /// Maps the <c>cache</c> health entry to a top-level RedisStatus (Healthy|Degraded).
    /// </summary>
    private static string? ResolveRedisStatus(HealthReport report)
    {
        if (!report.Entries.TryGetValue(RedisCacheHealthCheck.Name, out var entry))
            return null;

        return entry.Status switch
        {
            HealthStatus.Healthy => "Healthy",
            // Degraded (and any non-Healthy) → Degraded so ready never treats Redis as Unhealthy.
            _ => "Degraded",
        };
    }
}
