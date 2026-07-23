using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

public sealed class TseHealthTrendService : ITseHealthTrendService
{
    private static readonly ActivityEventType[] AlertActivityTypes =
    [
        ActivityEventType.TseFailoverActivated,
        ActivityEventType.TseFailoverNoBackup,
        ActivityEventType.TseFailoverReverted,
        ActivityEventType.TseFailoverStarted,
        ActivityEventType.TseFailoverFailed,
        ActivityEventType.TseFailoverBackupLowHealth,
    ];

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly ILogger<TseHealthTrendService> _logger;

    public TseHealthTrendService(
        AppDbContext db,
        IOptionsMonitor<TseOptions> tseOptions,
        ILogger<TseHealthTrendService> logger)
    {
        _db = db;
        _tseOptions = tseOptions;
        _logger = logger;
    }

    public async Task TryRecordSampleAsync(
        TseDevice device,
        int healthScore,
        TseHealthStatus status,
        string? message,
        DateTime checkedAtUtc,
        int? responseTimeMs = null,
        CancellationToken cancellationToken = default)
    {
        if (device.Id == Guid.Empty)
            return;

        var opts = _tseOptions.CurrentValue;
        var minInterval = TimeSpan.FromSeconds(Math.Clamp(opts.HealthSampleMinIntervalSeconds, 60, 3600));
        var retentionDays = Math.Clamp(opts.HealthSampleRetentionDays, 7, 90);
        var slowMs = Math.Max(100, opts.HealthSlowResponseMs);

        try
        {
            var last = await _db.TseDeviceHealthSamples.AsNoTracking()
                .Where(s => s.DeviceId == device.Id)
                .OrderByDescending(s => s.CheckedAtUtc)
                .Select(s => new { s.CheckedAtUtc, s.HealthScore, s.HealthStatus })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            var scoreChanged = last is null
                               || last.HealthScore != healthScore
                               || last.HealthStatus != status;
            var intervalElapsed = last is null || checkedAtUtc - last.CheckedAtUtc >= minInterval;
            var slowSpike = responseTimeMs is >= 0 && responseTimeMs.Value >= slowMs;

            if (!scoreChanged && !intervalElapsed && !slowSpike)
                return;

            _db.TseDeviceHealthSamples.Add(new TseDeviceHealthSample
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                TenantId = device.TenantId,
                CheckedAtUtc = checkedAtUtc,
                HealthScore = Math.Clamp(healthScore, 0, 100),
                HealthStatus = status,
                Message = Truncate(message, 500),
                IsPrimary = device.IsPrimary,
                IsBackup = device.IsBackup,
                ResponseTimeMs = responseTimeMs is < 0 ? null : responseTimeMs,
            });

            // Opportunistic retention prune (bounded delete).
            var cutoff = checkedAtUtc.AddDays(-retentionDays);
            var stale = await _db.TseDeviceHealthSamples
                .Where(s => s.DeviceId == device.Id && s.CheckedAtUtc < cutoff)
                .OrderBy(s => s.CheckedAtUtc)
                .Take(50)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (stale.Count > 0)
                _db.TseDeviceHealthSamples.RemoveRange(stale);

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record TSE health sample for device {DeviceId}", device.Id);
        }
    }

    public async Task<TseHealthReportDto> GenerateHealthReportAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        var opts = _tseOptions.CurrentValue;
        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);

        var devices = await LoadTenantDevicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var active = devices.Where(d => d.IsActive).ToList();
        var scores = active.Select(d => (double)d.HealthScore).ToList();

        var summaries = active
            .OrderByDescending(d => d.IsPrimary)
            .ThenBy(d => d.SerialNumber)
            .Select(d => new TseDeviceHealthSummaryDto
            {
                DeviceId = d.Id,
                VendorDeviceId = d.DeviceId,
                SerialNumber = d.SerialNumber,
                IsPrimary = d.IsPrimary,
                IsBackup = d.IsBackup,
                IsFailoverActive = d.IsFailoverActive,
                HealthStatus = d.HealthStatus.ToString(),
                HealthScore = d.HealthScore,
                HealthMessage = d.HealthMessage,
                LastHealthCheck = d.LastHealthCheck,
            })
            .ToList();

        var report = new TseHealthReportDto
        {
            TenantId = tenantId,
            TenantName = tenant?.Name,
            TenantSlug = tenant?.Slug,
            GeneratedAt = DateTime.UtcNow,
            TotalDevices = active.Count,
            HealthyDevices = active.Count(d => d.HealthStatus == TseHealthStatus.Healthy),
            DegradedDevices = active.Count(d => d.HealthStatus == TseHealthStatus.Degraded),
            UnhealthyDevices = active.Count(d =>
                d.HealthStatus is TseHealthStatus.Unhealthy or TseHealthStatus.Offline
                    or TseHealthStatus.Expired or TseHealthStatus.Revoked),
            AverageHealthScore = scores.Count == 0 ? 0 : Math.Round(scores.Average(), 1),
            MinHealthScore = scores.Count == 0 ? 0 : scores.Min(),
            MaxHealthScore = scores.Count == 0 ? 0 : scores.Max(),
            HealthyMinScore = opts.FailoverHealthyMinScore,
            DegradedMinScore = opts.FailoverDegradedMinScore,
            DeviceSummaries = summaries,
            RecentAlerts = await LoadRecentAlertsAsync(tenantId, cancellationToken).ConfigureAwait(false),
            Recommendations = BuildRecommendations(active, opts),
        };

        return report;
    }

    public async Task<IReadOnlyList<TseHealthTrendPointDto>> GetHealthTrendAsync(
        Guid tenantId,
        int days,
        Guid? deviceId = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        days = Math.Clamp(days, 1, 90);
        var fromUtc = DateTime.UtcNow.AddDays(-days);

        var deviceQuery = await LoadTenantDevicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var deviceIds = deviceQuery
            .Where(d => deviceId is null || d.Id == deviceId.Value)
            .Select(d => d.Id)
            .ToHashSet();

        if (deviceIds.Count == 0)
            return Array.Empty<TseHealthTrendPointDto>();

        var labels = deviceQuery.ToDictionary(
            d => d.Id,
            d => string.IsNullOrWhiteSpace(d.DeviceId) ? d.SerialNumber : d.DeviceId!);

        var samples = await _db.TseDeviceHealthSamples.AsNoTracking()
            .Where(s =>
                s.CheckedAtUtc >= fromUtc
                && deviceIds.Contains(s.DeviceId)
                && (s.TenantId == null || s.TenantId == tenantId))
            .OrderBy(s => s.CheckedAtUtc)
            .Take(5000)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return samples
            .Select(s => new TseHealthTrendPointDto
            {
                Date = s.CheckedAtUtc,
                DeviceId = s.DeviceId,
                DeviceLabel = labels.TryGetValue(s.DeviceId, out var label) ? label : s.DeviceId.ToString("D"),
                Score = s.HealthScore,
                HealthStatus = s.HealthStatus.ToString(),
            })
            .ToList();
    }

    private async Task<List<TseDevice>> LoadTenantDevicesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var registerIds = await _db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await _db.TseDevices.AsNoTracking()
            .Where(d =>
                d.TenantId == tenantId
                || (registerIds.Count > 0 && (
                    registerIds.Contains(d.KassenId)
                    || (d.CashRegisterId != null && registerIds.Contains(d.CashRegisterId.Value)))))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<TseHealthAlertDto>> LoadRecentAlertsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddDays(-14);
        var alerts = new List<TseHealthAlertDto>();

        var activities = await _db.ActivityEvents.AsNoTracking().IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId
                        && e.CreatedAtUtc >= since
                        && AlertActivityTypes.Contains(e.Type))
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(25)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var e in activities)
        {
            alerts.Add(new TseHealthAlertDto
            {
                Id = e.Id,
                Source = "Activity",
                Type = e.Type.ToString(),
                Severity = e.Severity,
                Title = e.Title,
                Description = e.Description,
                AtUtc = e.CreatedAtUtc,
            });
        }

        var logs = await _db.TseFailoverLogs.AsNoTracking().IgnoreQueryFilters()
            .Where(l => l.TenantId == tenantId && l.StartedAt >= since)
            .OrderByDescending(l => l.StartedAt)
            .Take(25)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var log in logs)
        {
            alerts.Add(new TseHealthAlertDto
            {
                Id = log.Id,
                Source = "FailoverLog",
                Type = log.FailoverType,
                Severity = log.IsSuccessful ? "Warning" : "Critical",
                Title = log.IsSuccessful
                    ? $"TSE failover ({log.FailoverType})"
                    : $"TSE failover failed ({log.FailoverType})",
                Description = string.IsNullOrWhiteSpace(log.ErrorMessage)
                    ? log.TriggerReason
                    : log.ErrorMessage,
                AtUtc = log.StartedAt,
            });
        }

        return alerts
            .OrderByDescending(a => a.AtUtc)
            .Take(30)
            .ToList();
    }

    private static List<TseHealthRecommendationDto> BuildRecommendations(
        IReadOnlyList<TseDevice> devices,
        TseOptions opts)
    {
        var list = new List<TseHealthRecommendationDto>();
        var healthyMin = opts.FailoverHealthyMinScore;
        var degradedMin = opts.FailoverDegradedMinScore;

        foreach (var primary in devices.Where(d => d.IsPrimary && d.IsActive))
        {
            var hasBackup = devices.Any(d =>
                d.IsActive && d.IsBackup && (d.PrimaryDeviceId == primary.Id || d.PrimaryDeviceId is null)
                && d.HealthStatus == TseHealthStatus.Healthy);

            if (!hasBackup)
            {
                list.Add(new TseHealthRecommendationDto
                {
                    Code = "NO_HEALTHY_BACKUP",
                    Severity = "Critical",
                    Message =
                        $"Primary {DeviceLabel(primary)} has no healthy backup device. Provision or repair a backup before failure.",
                    DeviceId = primary.Id,
                });
            }

            if (primary.HealthScore < degradedMin
                || primary.HealthStatus is TseHealthStatus.Unhealthy or TseHealthStatus.Offline
                    or TseHealthStatus.Expired or TseHealthStatus.Revoked)
            {
                list.Add(new TseHealthRecommendationDto
                {
                    Code = "PRIMARY_UNHEALTHY",
                    Severity = "Critical",
                    Message =
                        $"Primary {DeviceLabel(primary)} is unhealthy (score={primary.HealthScore}, status={primary.HealthStatus}). Investigate connectivity / certificate.",
                    DeviceId = primary.Id,
                });
            }
            else if (primary.HealthScore < healthyMin || primary.HealthStatus == TseHealthStatus.Degraded)
            {
                list.Add(new TseHealthRecommendationDto
                {
                    Code = "PRIMARY_DEGRADED",
                    Severity = "Warning",
                    Message =
                        $"Primary {DeviceLabel(primary)} is degraded (score={primary.HealthScore}). Monitor and prepare failover.",
                    DeviceId = primary.Id,
                });
            }

            if (primary.ExpiresAt is { } exp && exp <= DateTime.UtcNow.AddDays(30))
            {
                list.Add(new TseHealthRecommendationDto
                {
                    Code = "CERT_EXPIRING",
                    Severity = exp <= DateTime.UtcNow ? "Critical" : "Warning",
                    Message =
                        $"TSE certificate for {DeviceLabel(primary)} expires at {exp:u}. Renew before expiry.",
                    DeviceId = primary.Id,
                });
            }
        }

        foreach (var backup in devices.Where(d => d.IsFailoverActive && d.IsActive))
        {
            list.Add(new TseHealthRecommendationDto
            {
                Code = "FAILOVER_ACTIVE",
                Severity = "Warning",
                Message =
                    $"Backup {DeviceLabel(backup)} is actively signing (failover). Revert to primary when healthy.",
                DeviceId = backup.Id,
            });
        }

        if (list.Count == 0 && devices.Count > 0)
        {
            list.Add(new TseHealthRecommendationDto
            {
                Code = "OK",
                Severity = "Info",
                Message = "No critical TSE health issues detected for this tenant.",
            });
        }

        return list;
    }

    private static string DeviceLabel(TseDevice d) =>
        !string.IsNullOrWhiteSpace(d.DeviceId) ? d.DeviceId! : d.SerialNumber;

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}
