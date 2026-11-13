using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KasseAPI_Final.HealthChecks;

/// <summary>
/// Reports cached TSE operational health from <see cref="ITseHealthMonitor"/> (no device I/O).
/// Offline/degraded is <see cref="HealthStatus.Degraded"/> so load balancers keep routing while fiscal paths adapt.
/// </summary>
public sealed class TseCachedHealthCheck : IHealthCheck
{
    public const string Name = "tse";
    public const string DepsTag = "deps";

    private readonly ITseHealthMonitor _monitor;

    public TseCachedHealthCheck(ITseHealthMonitor monitor)
    {
        _monitor = monitor;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var snap = _monitor.Snapshot;
        var data = new Dictionary<string, object>
        {
            ["status"] = snap.Status.ToString(),
            ["consecutiveFailures"] = snap.ConsecutiveFailures,
            ["hasCompletedProbe"] = snap.HasCompletedProbe,
        };
        if (snap.LastCheckUtc is DateTime lastCheck)
            data["lastCheckUtc"] = lastCheck;
        if (snap.LastSuccessfulPingUtc is DateTime lastOk)
            data["lastSuccessfulPingUtc"] = lastOk;
        if (!string.IsNullOrWhiteSpace(snap.LastErrorMessageSafe))
            data["lastError"] = snap.LastErrorMessageSafe!;

        return Task.FromResult(snap.Status switch
        {
            TseOperationalHealth.Online => HealthCheckResult.Healthy("TSE online (cached).", data),
            TseOperationalHealth.Degraded => HealthCheckResult.Degraded("TSE degraded (cached).", data: data),
            TseOperationalHealth.Offline => HealthCheckResult.Degraded("TSE offline (cached).", data: data),
            _ => HealthCheckResult.Degraded($"TSE status unknown: {snap.Status}", data: data),
        });
    }
}
