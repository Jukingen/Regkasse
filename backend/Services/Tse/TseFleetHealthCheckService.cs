using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Fleet health aggregation for <c>/api/admin/tse/health</c>.
/// Implementation name avoids colliding with hosted <c>Services.TseHealthCheckService</c>.
/// </summary>
public sealed class TseFleetHealthCheckService : ITseHealthCheckService
{
    private readonly AppDbContext _db;
    private readonly ITseDeviceHealthCheckService _deviceHealth;
    private readonly ITseMetricsService _metrics;

    public TseFleetHealthCheckService(
        AppDbContext db,
        ITseDeviceHealthCheckService deviceHealth,
        ITseMetricsService metrics)
    {
        _db = db;
        _deviceHealth = deviceHealth;
        _metrics = metrics;
    }

    public Task<IReadOnlyList<TseHealthResult>> CheckAllDevicesAsync(
        CancellationToken cancellationToken = default) =>
        _deviceHealth.CheckAllDevicesAsync(cancellationToken);

    public async Task<TseFleetHealthStatusDto> GetOverallStatusAsync(
        bool liveProbe = true,
        CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTime.UtcNow;
        List<TseFleetDeviceHealthDto> devices;

        if (liveProbe)
        {
            var results = await _deviceHealth.CheckAllDevicesAsync(cancellationToken)
                .ConfigureAwait(false);
            var labels = await LoadDeviceLabelsAsync(
                    results.Select(r => r.DeviceId).Where(id => id != Guid.Empty).ToList(),
                    cancellationToken)
                .ConfigureAwait(false);

            devices = results
                .Where(r => r.DeviceId != Guid.Empty)
                .Select(r =>
                {
                    labels.TryGetValue(r.DeviceId, out var label);
                    return new TseFleetDeviceHealthDto
                    {
                        DeviceId = r.DeviceId,
                        TenantId = label?.TenantId,
                        SerialNumber = label?.SerialNumber,
                        Provider = label?.Provider,
                        Status = r.Status.ToString(),
                        HealthScore = r.HealthScore,
                        IsHealthy = r.IsHealthy,
                        Message = r.Message,
                        LastHealthCheckUtc = r.CheckedAt,
                        ResponseTimeMs = r.ResponseTimeMs,
                    };
                })
                .OrderBy(d => d.SerialNumber)
                .ThenBy(d => d.DeviceId)
                .ToList();
        }
        else
        {
            var rows = await _db.TseDevices
                .AsNoTracking()
                .Where(d => d.IsActive)
                .OrderBy(d => d.SerialNumber)
                .ThenBy(d => d.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            devices = rows.Select(MapCached).ToList();
        }

        var metrics = await _metrics.GetSummaryMetricsAsync(cancellationToken).ConfigureAwait(false);
        var healthy = devices.Count(d => d.IsHealthy);
        var degraded = devices.Count(d =>
            !d.IsHealthy
            && string.Equals(d.Status, nameof(TseHealthStatus.Degraded), StringComparison.OrdinalIgnoreCase));
        var unhealthy = devices.Count - healthy - degraded;

        return new TseFleetHealthStatusDto
        {
            Status = ResolveOverallStatus(devices),
            CheckedAtUtc = checkedAt,
            LiveProbe = liveProbe,
            DeviceCount = devices.Count,
            HealthyCount = healthy,
            DegradedCount = degraded,
            UnhealthyCount = Math.Max(0, unhealthy),
            Devices = devices,
            Metrics = metrics,
        };
    }

    private async Task<Dictionary<Guid, DeviceLabel>> LoadDeviceLabelsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, DeviceLabel>();

        var rows = await _db.TseDevices
            .AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .Select(d => new DeviceLabel(d.Id, d.TenantId, d.SerialNumber, d.Provider ?? d.DeviceType))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(r => r.Id);
    }

    private static TseFleetDeviceHealthDto MapCached(TseDevice d) =>
        new()
        {
            DeviceId = d.Id,
            TenantId = d.TenantId,
            SerialNumber = d.SerialNumber,
            Provider = d.Provider ?? d.DeviceType,
            Status = d.HealthStatus.ToString(),
            HealthScore = d.HealthScore,
            // Cached path mirrors probe: Healthy status only counts as IsHealthy.
            IsHealthy = d.HealthStatus == TseHealthStatus.Healthy,
            Message = d.HealthMessage ?? string.Empty,
            LastHealthCheckUtc = d.LastHealthCheck,
        };

    /// <summary>
    /// Matches the monitoring contract: all healthy → healthy; otherwise degraded,
    /// unless every device is hard-failed → unhealthy.
    /// </summary>
    internal static string ResolveOverallStatus(IReadOnlyList<TseFleetDeviceHealthDto> devices)
    {
        if (devices.Count == 0)
            return "healthy";

        if (devices.All(d => d.IsHealthy))
            return "healthy";

        var hardFail = devices.All(d =>
            string.Equals(d.Status, nameof(TseHealthStatus.Offline), StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.Status, nameof(TseHealthStatus.Unhealthy), StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.Status, nameof(TseHealthStatus.Expired), StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.Status, nameof(TseHealthStatus.Revoked), StringComparison.OrdinalIgnoreCase));

        return hardFail ? "unhealthy" : "degraded";
    }

    private sealed record DeviceLabel(Guid Id, Guid? TenantId, string SerialNumber, string? Provider);
}
