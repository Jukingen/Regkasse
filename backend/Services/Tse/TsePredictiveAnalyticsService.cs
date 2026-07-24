using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Rule-based TSE failure / health forecasting from probe samples and device metadata.
/// Diagnostic only — not a certified predictive model.
/// </summary>
public sealed class TsePredictiveAnalyticsService : ITsePredictiveAnalyticsService
{
    private const int MaxForecastDays = 90;
    private const int MinSamplesForTrend = 3;

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IActivityEventPublisher _activity;
    private readonly ILogger<TsePredictiveAnalyticsService> _logger;

    public TsePredictiveAnalyticsService(
        AppDbContext db,
        IOptionsMonitor<TseOptions> tseOptions,
        IActivityEventPublisher activity,
        ILogger<TsePredictiveAnalyticsService> logger)
    {
        _db = db;
        _tseOptions = tseOptions;
        _activity = activity;
        _logger = logger;
    }

    public async Task<TsePredictionResultDto> PredictFailureAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default) =>
        await PredictFailureCoreAsync(deviceId, publishAlert: true, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<TseRiskFactorDto>> IdentifyRiskFactorsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        var exists = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");

        var devices = await LoadTenantDevicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var active = devices.Where(d => d.IsActive).ToList();
        if (active.Count == 0)
            return Array.Empty<TseRiskFactorDto>();

        var aggregated = new List<TseRiskFactorDto>();
        foreach (var device in active.OrderByDescending(d => d.IsPrimary).ThenBy(d => d.SerialNumber))
        {
            var prediction = await PredictFailureCoreAsync(device.Id, publishAlert: false, cancellationToken)
                .ConfigureAwait(false);
            foreach (var factor in prediction.RiskFactors.Where(f => f.Impact >= 15))
            {
                aggregated.Add(factor);
            }
        }

        // Tenant-level: no healthy backup for primaries
        var opts = _tseOptions.CurrentValue;
        foreach (var primary in active.Where(d => d.IsPrimary))
        {
            var hasHealthyBackup = active.Any(d =>
                d.IsBackup
                && (d.PrimaryDeviceId == primary.Id || d.PrimaryDeviceId is null)
                && d.HealthStatus == TseHealthStatus.Healthy
                && d.HealthScore >= opts.FailoverHealthyMinScore);

            if (!hasHealthyBackup)
            {
                aggregated.Add(new TseRiskFactorDto
                {
                    Code = "no_healthy_backup",
                    Name = "No healthy backup",
                    Impact = 55,
                    Description =
                        $"Primary {DeviceLabel(primary)} has no healthy backup device for failover.",
                    IsActionable = true,
                    RecommendedAction = "Provision or repair a backup TSE",
                    DeviceId = primary.Id,
                });
            }
        }

        return aggregated
            .OrderByDescending(f => f.Impact)
            .ThenBy(f => f.Name)
            .Take(40)
            .ToList();
    }

    private async Task<TsePredictionResultDto> PredictFailureCoreAsync(
        Guid deviceId,
        bool publishAlert,
        CancellationToken cancellationToken)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("deviceId is required.", nameof(deviceId));

        var device = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            .ConfigureAwait(false);
        if (device is null)
            throw new KeyNotFoundException($"TSE device {deviceId} was not found.");

        var opts = _tseOptions.CurrentValue;
        var lookbackDays = Math.Clamp(opts.PredictiveLookbackDays, 3, 90);
        var fromUtc = DateTime.UtcNow.AddDays(-lookbackDays);

        var samples = await _db.TseDeviceHealthSamples.AsNoTracking()
            .Where(s => s.DeviceId == deviceId && s.CheckedAtUtc >= fromUtc)
            .OrderBy(s => s.CheckedAtUtc)
            .Take(2000)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var failoverCount = 0;
        if (device.TenantId is { } tenantId)
        {
            failoverCount = await _db.TseFailoverLogs.AsNoTracking().IgnoreQueryFilters()
                .CountAsync(
                    l => l.TenantId == tenantId
                         && (l.PrimaryDeviceId == deviceId || l.BackupDeviceId == deviceId)
                         && l.StartedAt >= fromUtc,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var trendPerDay = ComputeScoreTrendPerDay(samples);
        var factors = BuildDeviceRiskFactors(device, samples, trendPerDay, failoverCount, opts);
        var probability = Math.Clamp(Math.Round(factors.Sum(f => f.Impact), 1), 0, 100);
        var riskLevel = ToRiskLevel(probability, opts);
        var requiresAction = riskLevel is TsePredictiveRiskLevels.High or TsePredictiveRiskLevels.Critical
                             || device.HealthStatus is TseHealthStatus.Unhealthy or TseHealthStatus.Offline
                                 or TseHealthStatus.Expired or TseHealthStatus.Revoked;

        var predictedFailure = EstimateFailureDate(
            device.HealthScore,
            trendPerDay,
            opts.FailoverDegradedMinScore,
            probability,
            riskLevel);

        var recommendations = BuildRecommendations(factors, riskLevel, device);

        var result = new TsePredictionResultDto
        {
            DeviceId = device.Id,
            DeviceLabel = DeviceLabel(device),
            TenantId = device.TenantId,
            GeneratedAt = DateTime.UtcNow,
            FailureProbability = probability,
            RiskLevel = riskLevel,
            PredictedFailureDate = predictedFailure,
            CurrentHealthScore = device.HealthScore,
            CurrentHealthStatus = device.HealthStatus.ToString(),
            HealthTrendPerDay = Math.Round(trendPerDay, 2),
            SampleCount = samples.Count,
            RiskFactors = factors,
            Recommendations = recommendations,
            RequiresImmediateAction = requiresAction,
        };

        if (publishAlert && requiresAction && device.TenantId is { } tid && tid != Guid.Empty)
        {
            await _activity.TryPublishAsync(
                    tid,
                    ActivityEventType.TsePredictiveFailureRisk,
                    new
                    {
                        DeviceId = device.Id.ToString("D"),
                        DeviceLabel = result.DeviceLabel,
                        result.FailureProbability,
                        result.RiskLevel,
                        PredictedFailureDate = predictedFailure,
                        Codes = factors.Select(f => f.Code).ToList(),
                    },
                    actorUserId: "system",
                    dedupKey: $"tse-predict-fail:{device.Id:N}:{DateTime.UtcNow:yyyyMMdd}",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            result.AlertPublished = true;
        }

        _logger.LogInformation(
            "TSE failure prediction DeviceId={DeviceId} Risk={Risk} Probability={Probability} Samples={Samples}",
            deviceId,
            riskLevel,
            probability,
            samples.Count);

        return result;
    }

    public async Task<TseHealthPredictionDto> ForecastHealthAsync(
        Guid deviceId,
        int days,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("deviceId is required.", nameof(deviceId));

        days = Math.Clamp(days, 1, MaxForecastDays);

        var device = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            .ConfigureAwait(false);
        if (device is null)
            throw new KeyNotFoundException($"TSE device {deviceId} was not found.");

        var opts = _tseOptions.CurrentValue;
        var lookbackDays = Math.Clamp(opts.PredictiveLookbackDays, 3, 90);
        var fromUtc = DateTime.UtcNow.AddDays(-lookbackDays);

        var samples = await _db.TseDeviceHealthSamples.AsNoTracking()
            .Where(s => s.DeviceId == deviceId && s.CheckedAtUtc >= fromUtc)
            .OrderBy(s => s.CheckedAtUtc)
            .Take(2000)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var trendPerDay = ComputeScoreTrendPerDay(samples);
        // If no trend samples, assume slight decay toward current score stability.
        if (samples.Count < MinSamplesForTrend)
            trendPerDay = device.HealthScore < opts.FailoverHealthyMinScore ? -1.0 : 0;

        var now = DateTime.UtcNow.Date;
        var points = new List<TseHealthForecastPointDto>(days);
        DateTime? breachDate = null;
        var degradedMin = opts.FailoverDegradedMinScore;

        for (var i = 1; i <= days; i++)
        {
            var score = (int)Math.Clamp(
                Math.Round(device.HealthScore + trendPerDay * i),
                0,
                100);
            var status = ScoreToStatus(score, opts);
            var date = now.AddDays(i);
            points.Add(new TseHealthForecastPointDto
            {
                Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
                PredictedScore = score,
                PredictedStatus = status,
            });

            if (breachDate is null && score < degradedMin)
                breachDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        var horizonScore = points.Count == 0 ? device.HealthScore : points[^1].PredictedScore;
        var horizonRisk = ToRiskLevel(Math.Clamp(100 - horizonScore, 0, 100), opts);

        return new TseHealthPredictionDto
        {
            DeviceId = device.Id,
            DeviceLabel = DeviceLabel(device),
            TenantId = device.TenantId,
            GeneratedAt = DateTime.UtcNow,
            ForecastDays = days,
            CurrentHealthScore = device.HealthScore,
            HealthTrendPerDay = Math.Round(trendPerDay, 2),
            PredictedHealthScoreAtHorizon = horizonScore,
            PredictedRiskLevel = horizonRisk,
            PredictedBreachDate = breachDate,
            HealthyMinScore = opts.FailoverHealthyMinScore,
            DegradedMinScore = degradedMin,
            ForecastPoints = points,
        };
    }

    private async Task<List<TseDevice>> LoadTenantDevicesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var registerIds = await _db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .Where(d =>
                d.TenantId == tenantId
                || (registerIds.Count > 0 && (
                    registerIds.Contains(d.KassenId)
                    || (d.CashRegisterId != null && registerIds.Contains(d.CashRegisterId.Value)))))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static List<TseRiskFactorDto> BuildDeviceRiskFactors(
        TseDevice device,
        IReadOnlyList<TseDeviceHealthSample> samples,
        double trendPerDay,
        int failoverCount,
        TseOptions opts)
    {
        var factors = new List<TseRiskFactorDto>();
        var slowMs = Math.Max(100, opts.HealthSlowResponseMs);
        var criticalMs = Math.Max(slowMs, opts.HealthCriticalResponseMs);
        var certWarnDays = Math.Clamp(opts.CertificateExpiringSoonDays, 7, 90);

        // Current health score gap
        if (device.HealthScore < opts.FailoverHealthyMinScore)
        {
            var gap = opts.FailoverHealthyMinScore - device.HealthScore;
            var impact = device.HealthScore < opts.FailoverDegradedMinScore
                ? Math.Min(70, 40 + gap * 0.5)
                : Math.Min(45, 20 + gap * 0.4);
            factors.Add(new TseRiskFactorDto
            {
                Code = "low_health_score",
                Name = "Low health score",
                Impact = Math.Round(impact, 1),
                Description =
                    $"Current health score is {device.HealthScore} (healthy≥{opts.FailoverHealthyMinScore}, status={device.HealthStatus}).",
                IsActionable = true,
                RecommendedAction = "Investigate connectivity and certificate",
                DeviceId = device.Id,
            });
        }

        if (device.HealthStatus is TseHealthStatus.Unhealthy or TseHealthStatus.Offline
            or TseHealthStatus.Expired or TseHealthStatus.Revoked)
        {
            factors.Add(new TseRiskFactorDto
            {
                Code = "unhealthy_status",
                Name = "Unhealthy device status",
                Impact = 75,
                Description = $"Device status is {device.HealthStatus}. Signing may already be impaired.",
                IsActionable = true,
                RecommendedAction = "Trigger failover or repair primary",
                DeviceId = device.Id,
            });
        }
        else if (device.HealthStatus == TseHealthStatus.Degraded)
        {
            factors.Add(new TseRiskFactorDto
            {
                Code = "degraded_status",
                Name = "Degraded device status",
                Impact = 35,
                Description = "Device is degraded; failure risk is elevated.",
                IsActionable = true,
                RecommendedAction = "Prepare backup and monitor probes",
                DeviceId = device.Id,
            });
        }

        // Declining trend
        if (trendPerDay <= -opts.PredictiveDeclinePerDayWarning)
        {
            var impact = Math.Min(50, Math.Abs(trendPerDay) * 12);
            factors.Add(new TseRiskFactorDto
            {
                Code = "declining_health_trend",
                Name = "Declining health trend",
                Impact = Math.Round(impact, 1),
                Description =
                    $"Health score trend is {trendPerDay:0.##} points/day over recent probes.",
                IsActionable = true,
                RecommendedAction = "Review recent probe failures",
                DeviceId = device.Id,
            });
        }

        // Probe error rate
        if (samples.Count >= MinSamplesForTrend)
        {
            var failed = samples.Count(s =>
                s.HealthStatus is not (TseHealthStatus.Healthy or TseHealthStatus.Degraded));
            var errorRate = 100.0 * failed / samples.Count;
            if (errorRate >= opts.HealthErrorRateWarningPercent)
            {
                factors.Add(new TseRiskFactorDto
                {
                    Code = "high_probe_error_rate",
                    Name = "High probe error rate",
                    Impact = Math.Round(Math.Min(55, errorRate * 0.6), 1),
                    Description =
                        $"Probe failure rate {errorRate:0.#}% ({failed}/{samples.Count}) in lookback window.",
                    IsActionable = true,
                    RecommendedAction = "Check network / vendor API",
                    DeviceId = device.Id,
                });
            }

            var timed = samples.Where(s => s.ResponseTimeMs is > 0).Select(s => s.ResponseTimeMs!.Value).ToList();
            if (timed.Count >= MinSamplesForTrend)
            {
                var avg = timed.Average();
                if (avg >= criticalMs)
                {
                    factors.Add(new TseRiskFactorDto
                    {
                        Code = "critical_latency",
                        Name = "Critical probe latency",
                        Impact = 45,
                        Description = $"Average probe latency {avg:0} ms (critical≥{criticalMs}).",
                        IsActionable = true,
                        RecommendedAction = "Escalate vendor / hardware latency",
                        DeviceId = device.Id,
                    });
                }
                else if (avg >= slowMs)
                {
                    factors.Add(new TseRiskFactorDto
                    {
                        Code = "slow_latency",
                        Name = "Slow probe latency",
                        Impact = 25,
                        Description = $"Average probe latency {avg:0} ms (slow≥{slowMs}).",
                        IsActionable = true,
                        RecommendedAction = "Monitor latency trend",
                        DeviceId = device.Id,
                    });
                }
            }
        }

        // Certificate expiry
        if (device.ExpiresAt is { } exp)
        {
            var daysLeft = (exp - DateTime.UtcNow).TotalDays;
            if (daysLeft <= 0)
            {
                factors.Add(new TseRiskFactorDto
                {
                    Code = "certificate_expired",
                    Name = "Certificate expired",
                    Impact = 90,
                    Description = $"Signing certificate expired at {exp:u}.",
                    IsActionable = true,
                    RecommendedAction = "Renew certificate immediately",
                    DeviceId = device.Id,
                });
            }
            else if (daysLeft <= certWarnDays)
            {
                var impact = Math.Min(70, 25 + (certWarnDays - daysLeft));
                factors.Add(new TseRiskFactorDto
                {
                    Code = "certificate_expiring",
                    Name = "Certificate expiring soon",
                    Impact = Math.Round(impact, 1),
                    Description = $"Certificate expires in {daysLeft:0.#} days ({exp:u}).",
                    IsActionable = true,
                    RecommendedAction = "Schedule certificate renewal",
                    DeviceId = device.Id,
                });
            }
        }

        // Memory
        if (string.Equals(device.MemoryStatus, "FULL", StringComparison.OrdinalIgnoreCase))
        {
            factors.Add(new TseRiskFactorDto
            {
                Code = "memory_full",
                Name = "TSE memory full",
                Impact = 65,
                Description = "Device reports memory status FULL.",
                IsActionable = true,
                RecommendedAction = "Export / rotate device storage",
                DeviceId = device.Id,
            });
        }
        else if (string.Equals(device.MemoryStatus, "LOW", StringComparison.OrdinalIgnoreCase))
        {
            factors.Add(new TseRiskFactorDto
            {
                Code = "memory_low",
                Name = "TSE memory low",
                Impact = 30,
                Description = "Device reports memory status LOW.",
                IsActionable = true,
                RecommendedAction = "Plan storage maintenance",
                DeviceId = device.Id,
            });
        }

        if (!device.IsConnected)
        {
            factors.Add(new TseRiskFactorDto
            {
                Code = "disconnected",
                Name = "Device disconnected",
                Impact = 50,
                Description = "Device IsConnected=false.",
                IsActionable = true,
                RecommendedAction = "Restore device connectivity",
                DeviceId = device.Id,
            });
        }

        if (failoverCount >= opts.PredictiveFailoverCountWarning)
        {
            factors.Add(new TseRiskFactorDto
            {
                Code = "frequent_failovers",
                Name = "Frequent failovers",
                Impact = Math.Min(40, 15 + failoverCount * 5),
                Description = $"{failoverCount} failover event(s) involving this device in the lookback window.",
                IsActionable = true,
                RecommendedAction = "Stabilize primary before reverting",
                DeviceId = device.Id,
            });
        }

        if (device.IsFailoverActive)
        {
            factors.Add(new TseRiskFactorDto
            {
                Code = "failover_active",
                Name = "Failover currently active",
                Impact = 20,
                Description = "Backup is actively signing; primary recovery still pending.",
                IsActionable = true,
                RecommendedAction = "Repair primary and revert failover",
                DeviceId = device.Id,
            });
        }

        if (factors.Count == 0)
        {
            factors.Add(new TseRiskFactorDto
            {
                Code = "stable",
                Name = "Stable profile",
                Impact = 5,
                Description = "No elevated predictive risk factors detected from recent samples.",
                IsActionable = false,
                RecommendedAction = null,
                DeviceId = device.Id,
            });
        }

        return factors.OrderByDescending(f => f.Impact).ToList();
    }

    private static IReadOnlyList<string> BuildRecommendations(
        IReadOnlyList<TseRiskFactorDto> factors,
        string riskLevel,
        TseDevice device)
    {
        var list = new List<string>();
        foreach (var f in factors.Where(f => f.IsActionable && !string.IsNullOrWhiteSpace(f.RecommendedAction)))
        {
            if (!list.Contains(f.RecommendedAction!, StringComparer.Ordinal))
                list.Add(f.RecommendedAction!);
        }

        if (riskLevel is TsePredictiveRiskLevels.High or TsePredictiveRiskLevels.Critical
            && device.IsPrimary
            && !list.Any(r => r.Contains("failover", StringComparison.OrdinalIgnoreCase)))
        {
            list.Insert(0, "Validate backup TSE readiness and consider controlled failover");
        }

        if (list.Count == 0)
            list.Add("Continue routine health monitoring");

        return list;
    }

    private static double ComputeScoreTrendPerDay(IReadOnlyList<TseDeviceHealthSample> samples)
    {
        if (samples.Count < MinSamplesForTrend)
            return 0;

        // Simple linear regression: score ~ a + b * daysFromStart
        var origin = samples[0].CheckedAtUtc;
        double sumX = 0, sumY = 0, sumXy = 0, sumXx = 0;
        var n = samples.Count;
        foreach (var s in samples)
        {
            var x = (s.CheckedAtUtc - origin).TotalDays;
            var y = s.HealthScore;
            sumX += x;
            sumY += y;
            sumXy += x * y;
            sumXx += x * x;
        }

        var denom = n * sumXx - sumX * sumX;
        if (Math.Abs(denom) < 1e-9)
            return 0;

        return (n * sumXy - sumX * sumY) / denom;
    }

    private static DateTime? EstimateFailureDate(
        int currentScore,
        double trendPerDay,
        int degradedMin,
        double probability,
        string riskLevel)
    {
        if (riskLevel == TsePredictiveRiskLevels.Low && probability < 25)
            return null;

        if (currentScore < degradedMin)
            return DateTime.UtcNow.Date;

        if (trendPerDay >= -0.05)
        {
            // No clear decline — soft estimate from probability
            if (probability >= 70)
                return DateTime.UtcNow.Date.AddDays(3);
            if (probability >= 40)
                return DateTime.UtcNow.Date.AddDays(14);
            return DateTime.UtcNow.Date.AddDays(45);
        }

        var daysUntilBreach = (currentScore - degradedMin) / Math.Abs(trendPerDay);
        var days = Math.Clamp(daysUntilBreach, 0.5, 180);
        return DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(days), DateTimeKind.Utc);
    }

    private static string ToRiskLevel(double probability, TseOptions opts)
    {
        if (probability >= opts.PredictiveCriticalProbability)
            return TsePredictiveRiskLevels.Critical;
        if (probability >= opts.PredictiveHighProbability)
            return TsePredictiveRiskLevels.High;
        if (probability >= opts.PredictiveMediumProbability)
            return TsePredictiveRiskLevels.Medium;
        return TsePredictiveRiskLevels.Low;
    }

    private static string ScoreToStatus(int score, TseOptions opts)
    {
        if (score >= opts.FailoverHealthyMinScore)
            return TseHealthStatus.Healthy.ToString();
        if (score >= opts.FailoverDegradedMinScore)
            return TseHealthStatus.Degraded.ToString();
        if (score <= 0)
            return TseHealthStatus.Offline.ToString();
        return TseHealthStatus.Unhealthy.ToString();
    }

    private static string DeviceLabel(TseDevice d) =>
        !string.IsNullOrWhiteSpace(d.DeviceId) ? d.DeviceId! : d.SerialNumber;
}
