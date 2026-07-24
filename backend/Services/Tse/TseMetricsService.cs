using System.Globalization;
using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Builds TSE fleet JSON summary and Prometheus text from last-known device health columns
/// (scrape-safe; does not invoke live vendor probes).
/// </summary>
public sealed class TseMetricsService : ITseMetricsService
{
    private readonly AppDbContext _db;

    public TseMetricsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TseHealthMetricsSummaryDto> GetSummaryMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var devices = await _db.TseDevices
            .AsNoTracking()
            .Where(d => d.IsActive)
            .Select(d => new DeviceSnap(
                d.Id,
                d.TenantId,
                d.Provider ?? d.DeviceType,
                d.HealthStatus,
                d.HealthScore,
                d.LastHealthCheck,
                d.IsPrimary,
                d.IsBackup,
                d.IsFailoverActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return BuildSummary(devices, now);
    }

    public async Task<string> GetPrometheusMetricsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var devices = await _db.TseDevices
            .AsNoTracking()
            .Where(d => d.IsActive)
            .Select(d => new DeviceSnap(
                d.Id,
                d.TenantId,
                d.Provider ?? d.DeviceType,
                d.HealthStatus,
                d.HealthScore,
                d.LastHealthCheck,
                d.IsPrimary,
                d.IsBackup,
                d.IsFailoverActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var summary = BuildSummary(devices, now);
        var sb = new StringBuilder(2048);

        WriteHelpType(sb, "tse_devices_total", "gauge", "Active TSE devices by health status");
        foreach (var (status, count) in summary.DevicesByStatus.OrderBy(kv => kv.Key))
        {
            sb.Append("tse_devices_total{status=\"")
                .Append(EscapeLabel(status.ToLowerInvariant()))
                .Append("\"} ")
                .Append(count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        WriteHelpType(sb, "tse_devices_by_provider", "gauge", "Active TSE devices by provider");
        foreach (var (provider, count) in summary.DevicesByProvider.OrderBy(kv => kv.Key))
        {
            sb.Append("tse_devices_by_provider{provider=\"")
                .Append(EscapeLabel(provider))
                .Append("\"} ")
                .Append(count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        WriteHelpType(sb, "tse_device_health_score", "gauge", "Per-device TSE health score (0-100)");
        foreach (var d in devices.OrderBy(x => x.Id))
        {
            sb.Append("tse_device_health_score{device_id=\"")
                .Append(EscapeLabel(d.Id.ToString("D")))
                .Append("\",provider=\"")
                .Append(EscapeLabel(d.Provider ?? "unknown"))
                .Append("\",tenant_id=\"")
                .Append(EscapeLabel(d.TenantId?.ToString("D") ?? "none"))
                .Append("\"} ")
                .Append(d.HealthScore.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        WriteHelpType(sb, "tse_average_health_score", "gauge", "Average TSE health score across active devices");
        sb.Append("tse_average_health_score ")
            .Append(summary.AverageHealthScore.ToString("0.###", CultureInfo.InvariantCulture))
            .Append('\n');

        WriteHelpType(sb, "tse_failover_active", "gauge", "Number of devices currently in active failover");
        sb.Append("tse_failover_active ")
            .Append(summary.ActiveFailoverCount.ToString(CultureInfo.InvariantCulture))
            .Append('\n');

        WriteHelpType(sb, "tse_primary_devices", "gauge", "Active primary TSE device count");
        sb.Append("tse_primary_devices ")
            .Append(summary.PrimaryDevices.ToString(CultureInfo.InvariantCulture))
            .Append('\n');

        WriteHelpType(sb, "tse_backup_devices", "gauge", "Active backup TSE device count");
        sb.Append("tse_backup_devices ")
            .Append(summary.BackupDevices.ToString(CultureInfo.InvariantCulture))
            .Append('\n');

        var fleet = ResolveFleetGauge(summary);
        WriteHelpType(
            sb,
            "tse_fleet_status",
            "gauge",
            "Overall fleet status: 1=healthy, 0.5=degraded, 0=unhealthy");
        sb.Append("tse_fleet_status ")
            .Append(fleet.ToString("0.###", CultureInfo.InvariantCulture))
            .Append('\n');

        WriteHelpType(
            sb,
            "tse_health_check_staleness_seconds",
            "gauge",
            "Seconds since the oldest last health check among active devices");
        sb.Append("tse_health_check_staleness_seconds ")
            .Append((summary.MaxStalenessSeconds ?? 0).ToString("0.###", CultureInfo.InvariantCulture))
            .Append('\n');

        return sb.ToString();
    }

    internal static TseHealthMetricsSummaryDto BuildSummary(
        IReadOnlyList<DeviceSnap> devices,
        DateTime nowUtc)
    {
        var byStatus = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byProvider = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var healthy = 0;
        var degraded = 0;
        var unhealthy = 0;
        var offline = 0;
        var expiredOrRevoked = 0;
        long scoreSum = 0;
        DateTime? oldest = null;

        foreach (var d in devices)
        {
            var statusKey = d.HealthStatus.ToString();
            byStatus[statusKey] = byStatus.TryGetValue(statusKey, out var c) ? c + 1 : 1;

            var provider = string.IsNullOrWhiteSpace(d.Provider) ? "unknown" : d.Provider!;
            byProvider[provider] = byProvider.TryGetValue(provider, out var pc) ? pc + 1 : 1;

            scoreSum += d.HealthScore;
            if (d.LastHealthCheckUtc is { } checkedAt)
            {
                if (oldest is null || checkedAt < oldest)
                    oldest = checkedAt;
            }

            switch (d.HealthStatus)
            {
                case TseHealthStatus.Healthy:
                    healthy++;
                    break;
                case TseHealthStatus.Degraded:
                    degraded++;
                    break;
                case TseHealthStatus.Offline:
                    offline++;
                    unhealthy++;
                    break;
                case TseHealthStatus.Expired:
                case TseHealthStatus.Revoked:
                    expiredOrRevoked++;
                    unhealthy++;
                    break;
                default:
                    unhealthy++;
                    break;
            }
        }

        double? staleness = null;
        if (oldest is not null)
            staleness = Math.Max(0, (nowUtc - oldest.Value).TotalSeconds);

        return new TseHealthMetricsSummaryDto
        {
            GeneratedAtUtc = nowUtc,
            ActiveDevices = devices.Count,
            HealthyDevices = healthy,
            DegradedDevices = degraded,
            UnhealthyDevices = unhealthy,
            OfflineDevices = offline,
            ExpiredOrRevokedDevices = expiredOrRevoked,
            AverageHealthScore = devices.Count == 0
                ? 100
                : Math.Round(scoreSum / (double)devices.Count, 3),
            ActiveFailoverCount = devices.Count(d => d.IsFailoverActive),
            PrimaryDevices = devices.Count(d => d.IsPrimary),
            BackupDevices = devices.Count(d => d.IsBackup),
            MaxStalenessSeconds = staleness,
            OldestHealthCheckUtc = oldest,
            DevicesByProvider = byProvider,
            DevicesByStatus = byStatus,
        };
    }

    internal static double ResolveFleetGauge(TseHealthMetricsSummaryDto summary)
    {
        if (summary.ActiveDevices == 0)
            return 1;
        if (summary.UnhealthyDevices == summary.ActiveDevices)
            return 0;
        if (summary.HealthyDevices == summary.ActiveDevices)
            return 1;
        return 0.5;
    }

    private static void WriteHelpType(StringBuilder sb, string name, string type, string help)
    {
        sb.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
        sb.Append("# TYPE ").Append(name).Append(' ').Append(type).Append('\n');
    }

    internal static string EscapeLabel(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    internal sealed record DeviceSnap(
        Guid Id,
        Guid? TenantId,
        string? Provider,
        TseHealthStatus HealthStatus,
        int HealthScore,
        DateTime? LastHealthCheckUtc,
        bool IsPrimary,
        bool IsBackup,
        bool IsFailoverActive);
}
