using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

public sealed class TsePerformanceService : ITsePerformanceService
{
    private const int MaxHistoryPoints = 200;
    private const int MinSamplesForAnomaly = 3;

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IActivityEventPublisher _activity;
    private readonly ILogger<TsePerformanceService> _logger;

    public TsePerformanceService(
        AppDbContext db,
        IOptionsMonitor<TseOptions> tseOptions,
        IActivityEventPublisher activity,
        ILogger<TsePerformanceService> logger)
    {
        _db = db;
        _tseOptions = tseOptions;
        _activity = activity;
        _logger = logger;
    }

    public async Task<TsePerformanceMetricsDto> GetPerformanceMetricsAsync(
        Guid deviceId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("deviceId is required.", nameof(deviceId));

        fromUtc = NormalizeUtc(fromUtc);
        toUtc = NormalizeUtc(toUtc);
        if (toUtc < fromUtc)
            (fromUtc, toUtc) = (toUtc, fromUtc);

        // Cap window to retention (avoid huge scans).
        var retentionDays = Math.Clamp(_tseOptions.CurrentValue.HealthSampleRetentionDays, 7, 90);
        var minFrom = DateTime.UtcNow.AddDays(-retentionDays);
        if (fromUtc < minFrom)
            fromUtc = minFrom;

        var opts = _tseOptions.CurrentValue;
        var slowMs = Math.Max(100, opts.HealthSlowResponseMs);
        var criticalMs = Math.Max(slowMs, opts.HealthCriticalResponseMs);

        var device = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            .ConfigureAwait(false);

        if (device is null)
            throw new KeyNotFoundException($"TSE device {deviceId} was not found.");

        var samples = await _db.TseDeviceHealthSamples.AsNoTracking()
            .Where(s => s.DeviceId == deviceId
                        && s.CheckedAtUtc >= fromUtc
                        && s.CheckedAtUtc <= toUtc)
            .OrderBy(s => s.CheckedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return BuildMetrics(device, samples, fromUtc, toUtc, slowMs, criticalMs);
    }

    public async Task<TsePerformanceAlertDto> CheckPerformanceAnomaliesAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("deviceId is required.", nameof(deviceId));

        var opts = _tseOptions.CurrentValue;
        var lookbackHours = Math.Clamp(opts.HealthPerformanceLookbackHours, 1, 168);
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddHours(-lookbackHours);

        TsePerformanceMetricsDto metrics;
        try
        {
            metrics = await GetPerformanceMetricsAsync(deviceId, fromUtc, toUtc, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            return new TsePerformanceAlertDto
            {
                DeviceId = deviceId,
                HasAnomaly = false,
                Severity = "Info",
                Message = "Device not found.",
            };
        }

        var codes = new List<string>();
        var severity = "Info";

        if (metrics.TotalRequests >= MinSamplesForAnomaly)
        {
            if (metrics.ErrorRate >= opts.HealthErrorRateCriticalPercent)
            {
                codes.Add("high_error_rate_critical");
                severity = "Critical";
            }
            else if (metrics.ErrorRate >= opts.HealthErrorRateWarningPercent)
            {
                codes.Add("high_error_rate");
                severity = MaxSeverity(severity, "Warning");
            }

            if (metrics.TimedSamples > 0)
            {
                if (metrics.MaxResponseTime >= metrics.CriticalThresholdMs
                    || metrics.AverageResponseTime >= metrics.CriticalThresholdMs)
                {
                    codes.Add("slow_response_critical");
                    severity = "Critical";
                }
                else if (metrics.AverageResponseTime >= metrics.SlowThresholdMs
                         || metrics.MaxResponseTime >= metrics.SlowThresholdMs)
                {
                    codes.Add("slow_response");
                    severity = MaxSeverity(severity, "Warning");
                }
            }
        }

        var hasAnomaly = codes.Count > 0;
        var message = hasAnomaly
            ? BuildAlertMessage(metrics, codes)
            : metrics.TotalRequests == 0
                ? "No health samples in lookback window."
                : "No performance anomalies detected.";

        var alert = new TsePerformanceAlertDto
        {
            DeviceId = deviceId,
            TenantId = metrics.TenantId,
            HasAnomaly = hasAnomaly,
            Severity = severity,
            Codes = codes,
            Message = message,
            Metrics = metrics,
        };

        if (!hasAnomaly || metrics.TenantId is not { } tenantId || tenantId == Guid.Empty)
            return alert;

        var published = false;
        if (codes.Any(c => c.StartsWith("slow_response", StringComparison.Ordinal)))
        {
            await _activity.TryPublishAsync(
                    tenantId,
                    ActivityEventType.TsePerformanceSlow,
                    new
                    {
                        DeviceId = deviceId.ToString("D"),
                        metrics.AverageResponseTime,
                        metrics.MaxResponseTime,
                        metrics.SlowThresholdMs,
                        metrics.CriticalThresholdMs,
                        Codes = codes,
                        Message = message,
                    },
                    actorUserId: "system",
                    dedupKey: $"tse-perf-slow:{deviceId:N}:{DateTime.UtcNow:yyyyMMddHH}",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            published = true;
        }

        if (codes.Any(c => c.StartsWith("high_error_rate", StringComparison.Ordinal)))
        {
            await _activity.TryPublishAsync(
                    tenantId,
                    ActivityEventType.TsePerformanceHighErrorRate,
                    new
                    {
                        DeviceId = deviceId.ToString("D"),
                        metrics.ErrorRate,
                        metrics.SuccessRate,
                        metrics.FailedRequests,
                        metrics.TotalRequests,
                        Codes = codes,
                        Message = message,
                    },
                    actorUserId: "system",
                    dedupKey: $"tse-perf-errors:{deviceId:N}:{DateTime.UtcNow:yyyyMMddHH}",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            published = true;
        }

        alert.AlertPublished = published;
        return alert;
    }

    public async Task ProcessPerformanceAnomaliesAsync(CancellationToken cancellationToken = default)
    {
        var deviceIds = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.IsActive && (d.IsPrimary || d.IsFailoverActive))
            .Select(d => d.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var deviceId in deviceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var alert = await CheckPerformanceAnomaliesAsync(deviceId, cancellationToken)
                    .ConfigureAwait(false);
                if (alert.HasAnomaly)
                {
                    _logger.LogWarning(
                        "TSE performance anomaly DeviceId={DeviceId} Severity={Severity} Codes={Codes} Message={Message}",
                        deviceId,
                        alert.Severity,
                        string.Join(',', alert.Codes),
                        alert.Message);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "TSE performance anomaly check failed for device {DeviceId}", deviceId);
            }
        }
    }

    private static TsePerformanceMetricsDto BuildMetrics(
        TseDevice device,
        IReadOnlyList<TseDeviceHealthSample> samples,
        DateTime fromUtc,
        DateTime toUtc,
        int slowMs,
        int criticalMs)
    {
        var total = samples.Count;
        var success = samples.Count(IsSuccessfulProbe);
        var failed = total - success;
        var timed = samples.Where(s => s.ResponseTimeMs is > 0).Select(s => (double)s.ResponseTimeMs!.Value).ToList();

        var history = samples
            .TakeLast(MaxHistoryPoints)
            .Select(s => new TsePerformancePointDto
            {
                Timestamp = s.CheckedAtUtc,
                ResponseTimeMs = s.ResponseTimeMs,
                Success = IsSuccessfulProbe(s),
                HealthScore = s.HealthScore,
                HealthStatus = s.HealthStatus.ToString(),
            })
            .ToList();

        return new TsePerformanceMetricsDto
        {
            DeviceId = device.Id,
            DeviceLabel = string.IsNullOrWhiteSpace(device.SerialNumber)
                ? device.DeviceId
                : device.SerialNumber,
            TenantId = device.TenantId,
            StartDate = fromUtc,
            EndDate = toUtc,
            AverageResponseTime = timed.Count == 0 ? 0 : Math.Round(timed.Average(), 2),
            MinResponseTime = timed.Count == 0 ? 0 : timed.Min(),
            MaxResponseTime = timed.Count == 0 ? 0 : timed.Max(),
            TimedSamples = timed.Count,
            TotalRequests = total,
            SuccessfulRequests = success,
            FailedRequests = failed,
            SuccessRate = total == 0 ? 0 : Math.Round(100.0 * success / total, 2),
            ErrorRate = total == 0 ? 0 : Math.Round(100.0 * failed / total, 2),
            SlowThresholdMs = slowMs,
            CriticalThresholdMs = criticalMs,
            PerformanceHistory = history,
        };
    }

    private static bool IsSuccessfulProbe(TseDeviceHealthSample sample) =>
        sample.HealthStatus is TseHealthStatus.Healthy or TseHealthStatus.Degraded;

    private static string BuildAlertMessage(TsePerformanceMetricsDto metrics, IReadOnlyList<string> codes)
    {
        var parts = new List<string>();
        if (codes.Any(c => c.Contains("slow", StringComparison.Ordinal)))
        {
            parts.Add(
                $"Avg {metrics.AverageResponseTime:0} ms / max {metrics.MaxResponseTime:0} ms "
                + $"(slow≥{metrics.SlowThresholdMs}, critical≥{metrics.CriticalThresholdMs}).");
        }

        if (codes.Any(c => c.Contains("error", StringComparison.Ordinal)))
        {
            parts.Add(
                $"Error rate {metrics.ErrorRate:0.##}% "
                + $"({metrics.FailedRequests}/{metrics.TotalRequests} failed probes).");
        }

        return string.Join(' ', parts);
    }

    private static string MaxSeverity(string current, string candidate)
    {
        static int Rank(string s) => s switch
        {
            "Critical" => 3,
            "Warning" => 2,
            _ => 1,
        };

        return Rank(candidate) > Rank(current) ? candidate : current;
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
