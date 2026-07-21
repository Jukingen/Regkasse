using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.HealthChecks;

/// <summary>
/// Reports cached NTP fiscal readiness from <see cref="INtpTimeSyncStatus"/> (no network NTP query).
/// Unsynchronized time is <see cref="HealthStatus.Degraded"/> — API stays up; online fiscal is gated elsewhere.
/// </summary>
public sealed class NtpCachedHealthCheck : IHealthCheck
{
    public const string Name = "ntp";
    public const string DepsTag = "deps";

    private readonly INtpTimeSyncStatus _ntpStatus;
    private readonly IOptionsMonitor<NtpSettings> _ntpSettings;

    public NtpCachedHealthCheck(
        INtpTimeSyncStatus ntpStatus,
        IOptionsMonitor<NtpSettings> ntpSettings)
    {
        _ntpStatus = ntpStatus;
        _ntpSettings = ntpSettings;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var settings = _ntpSettings.CurrentValue;
        var dto = _ntpStatus.BuildStatusDto(settings);
        var data = new Dictionary<string, object>
        {
            ["enabled"] = settings.Enabled,
            ["isSynchronized"] = dto.IsSynchronized,
            ["warningLevel"] = dto.WarningLevel ?? string.Empty,
        };
        if (dto.OffsetSeconds is double offset)
            data["offsetSeconds"] = offset;
        if (dto.LastSyncAt is DateTime lastSync)
            data["lastSyncAtUtc"] = lastSync;

        if (!settings.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("NTP checks disabled.", data));

        if (_ntpStatus.ShouldAllowOnlineFiscalPayment(settings, out var messageDe))
            return Task.FromResult(HealthCheckResult.Healthy("NTP OK for online fiscal (cached).", data));

        return Task.FromResult(HealthCheckResult.Degraded(
            string.IsNullOrWhiteSpace(messageDe)
                ? "NTP not synchronized for online fiscal (cached)."
                : messageDe,
            data: data));
    }
}
