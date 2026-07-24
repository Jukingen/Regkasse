using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Detects TSE operational anomalies via statistical baseline deviation
/// (lookback mean vs recent window). Not a trained ML model.
/// </summary>
public sealed class TseAnomalyDetectionService : ITseAnomalyDetectionService
{
    private const int BaselineDays = 14;
    private const int RecentHours = 6;
    private const int MinBaselineSamples = 5;
    private const double MinDeviationToPersist = 15;
    private const int DedupHours = 6;
    private const int MaxReportDays = 90;

    private readonly AppDbContext _db;
    private readonly IActivityEventPublisher _activity;
    private readonly ILogger<TseAnomalyDetectionService> _logger;

    public TseAnomalyDetectionService(
        AppDbContext db,
        IActivityEventPublisher activity,
        ILogger<TseAnomalyDetectionService> logger)
    {
        _db = db;
        _activity = activity;
        _logger = logger;
    }

    public async Task<TseAnomalyDashboardDto> GetDashboardAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var open = await _db.TseAnomalies.AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsResolved)
            .OrderByDescending(a => a.DetectedAt)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dtos = open.Select(MapAnomaly).ToList();
        return new TseAnomalyDashboardDto
        {
            TenantId = tenantId,
            TenantName = tenant.Name,
            CriticalCount = dtos.Count(a => a.Severity == TseAnomalySeverities.Critical),
            HighCount = dtos.Count(a => a.Severity == TseAnomalySeverities.High),
            MediumCount = dtos.Count(a => a.Severity == TseAnomalySeverities.Medium),
            LowCount = dtos.Count(a => a.Severity == TseAnomalySeverities.Low),
            InfoCount = dtos.Count(a => a.Severity == TseAnomalySeverities.Info),
            OpenCount = dtos.Count,
            Anomalies = dtos,
        };
    }

    public async Task<TseAnomalyResultDto> DetectAnomaliesAsync(
        Guid tenantId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var devices = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId && d.IsActive)
            .OrderByDescending(d => d.IsPrimary)
            .ThenBy(d => d.SerialNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var findings = new List<TseAnomaly>();
        foreach (var device in devices)
        {
            findings.AddRange(await DetectForDeviceCoreAsync(device, persist: false, cancellationToken)
                .ConfigureAwait(false));
        }

        findings.AddRange(await DetectTenantVolumeAsync(tenantId, persist: false, cancellationToken)
            .ConfigureAwait(false));

        var persisted = await PersistFindingsAsync(findings, actorUserId, cancellationToken)
            .ConfigureAwait(false);

        var result = BuildResult(tenantId, deviceId: null, persisted);
        if (result.RequiresAction)
        {
            await PublishAlertAsync(tenantId, result, actorUserId, cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    public async Task<TseAnomalyResultDto> DetectAnomaliesForDeviceAsync(
        Guid deviceId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("deviceId is required.", nameof(deviceId));

        var device = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            .ConfigureAwait(false);
        if (device is null)
            throw new KeyNotFoundException($"TSE device {deviceId} was not found.");
        if (device.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            throw new InvalidOperationException($"TSE device {deviceId} has no tenant.");

        var findings = await DetectForDeviceCoreAsync(device, persist: false, cancellationToken)
            .ConfigureAwait(false);
        var persisted = await PersistFindingsAsync(findings, actorUserId, cancellationToken)
            .ConfigureAwait(false);

        var result = BuildResult(tenantId, device.Id, persisted);
        if (result.RequiresAction)
        {
            await PublishAlertAsync(tenantId, result, actorUserId, cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    public async Task<TseAnomalyReportDto> GenerateAnomalyReportAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

        if (fromUtc.Kind == DateTimeKind.Unspecified)
            fromUtc = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
        if (toUtc.Kind == DateTimeKind.Unspecified)
            toUtc = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc);
        if (fromUtc > toUtc)
            (fromUtc, toUtc) = (toUtc, fromUtc);
        if ((toUtc - fromUtc).TotalDays > MaxReportDays)
            fromUtc = toUtc.AddDays(-MaxReportDays);

        var rows = await _db.TseAnomalies.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.DetectedAt >= fromUtc && a.DetectedAt <= toUtc)
            .OrderByDescending(a => a.DetectedAt)
            .Take(500)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dtos = rows.Select(MapAnomaly).ToList();
        var overall = dtos.Aggregate(TseAnomalySeverities.Info, (acc, a) =>
            TseAnomalySeverities.Max(acc, a.Severity));

        return new TseAnomalyReportDto
        {
            TenantId = tenantId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            TotalAnomalies = dtos.Count,
            OpenAnomalies = dtos.Count(a => !a.IsResolved),
            CriticalCount = dtos.Count(a => a.Severity == TseAnomalySeverities.Critical),
            HighCount = dtos.Count(a => a.Severity == TseAnomalySeverities.High),
            MediumCount = dtos.Count(a => a.Severity == TseAnomalySeverities.Medium),
            LowCount = dtos.Count(a => a.Severity == TseAnomalySeverities.Low),
            InfoCount = dtos.Count(a => a.Severity == TseAnomalySeverities.Info),
            OverallSeverity = overall,
            Anomalies = dtos,
            DiagnosticOnly = true,
        };
    }

    public async Task<bool> IsAnomalyDetectedAsync(
        Guid tenantId,
        string metricName,
        double value,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(metricName))
            return false;

        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var metric = metricName.Trim();

        var baseline = await ComputeTenantBaselineAsync(tenantId, metric, cancellationToken)
            .ConfigureAwait(false);
        if (baseline is null || baseline.Value.Expected <= 0 && Math.Abs(value) < 0.0001)
            return false;

        var expected = baseline.Value.Expected;
        var deviation = PercentDeviation(value, expected);
        return deviation >= MinDeviationToPersist;
    }

    public async Task<TseAnomalyDto> ResolveAnomalyAsync(
        Guid anomalyId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (anomalyId == Guid.Empty)
            throw new ArgumentException("anomalyId is required.", nameof(anomalyId));

        var row = await _db.TseAnomalies
            .FirstOrDefaultAsync(a => a.Id == anomalyId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"Anomaly {anomalyId} was not found.");

        if (!row.IsResolved)
        {
            row.IsResolved = true;
            row.ResolvedAt = DateTime.UtcNow;
            row.ResolvedBy = Truncate(actorUserId, 450);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return MapAnomaly(row);
    }

    private async Task<List<TseAnomaly>> DetectForDeviceCoreAsync(
        TseDevice device,
        bool persist,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var baselineFrom = now.AddDays(-BaselineDays);
        var recentFrom = now.AddHours(-RecentHours);

        var samples = await _db.TseDeviceHealthSamples.AsNoTracking()
            .Where(s => s.DeviceId == device.Id && s.CheckedAtUtc >= baselineFrom)
            .OrderBy(s => s.CheckedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var findings = new List<TseAnomaly>();
        if (samples.Count < MinBaselineSamples)
            return findings;

        var baselineSamples = samples.Where(s => s.CheckedAtUtc < recentFrom).ToList();
        var recentSamples = samples.Where(s => s.CheckedAtUtc >= recentFrom).ToList();
        if (baselineSamples.Count < MinBaselineSamples || recentSamples.Count == 0)
            return findings;

        // Health score (lower is worse — invert severity direction)
        var expectedScore = baselineSamples.Average(s => (double)s.HealthScore);
        var currentScore = recentSamples.Average(s => (double)s.HealthScore);
        MaybeAdd(
            findings,
            device,
            TseAnomalyMetrics.HealthScore,
            currentScore,
            expectedScore,
            higherIsWorse: false,
            description: $"Health score for {DeviceLabel(device)} is {currentScore:0.#} vs baseline {expectedScore:0.#}.",
            action: "Inspect device connectivity, certificates, and recent failover history.");

        // Response time (higher is worse)
        var baselineRt = baselineSamples
            .Where(s => s.ResponseTimeMs.HasValue)
            .Select(s => (double)s.ResponseTimeMs!.Value)
            .ToList();
        var recentRt = recentSamples
            .Where(s => s.ResponseTimeMs.HasValue)
            .Select(s => (double)s.ResponseTimeMs!.Value)
            .ToList();
        if (baselineRt.Count >= 3 && recentRt.Count > 0)
        {
            var expectedRt = baselineRt.Average();
            var currentRt = recentRt.Average();
            MaybeAdd(
                findings,
                device,
                TseAnomalyMetrics.ResponseTimeMs,
                currentRt,
                expectedRt,
                higherIsWorse: true,
                description: $"Probe latency for {DeviceLabel(device)} is {currentRt:0} ms vs baseline {expectedRt:0} ms.",
                action: "Check vendor API latency, network path, and device load.");
        }

        // Error rate = non-healthy share
        static double ErrorRate(IReadOnlyList<TseDeviceHealthSample> list) =>
            list.Count == 0
                ? 0
                : 100.0 * list.Count(s => s.HealthStatus != TseHealthStatus.Healthy) / list.Count;

        var expectedErr = ErrorRate(baselineSamples);
        var currentErr = ErrorRate(recentSamples);
        MaybeAdd(
            findings,
            device,
            TseAnomalyMetrics.ErrorRatePercent,
            currentErr,
            Math.Max(expectedErr, 0.5), // floor so tiny baselines still flag spikes
            higherIsWorse: true,
            description: $"Unhealthy probe rate for {DeviceLabel(device)} is {currentErr:0.#}% vs baseline {expectedErr:0.#}%.",
            action: "Review health samples and consider failover readiness.");

        if (persist)
        {
            await PersistFindingsAsync(findings, actorUserId: null, cancellationToken)
                .ConfigureAwait(false);
        }

        return findings;
    }

    private async Task<List<TseAnomaly>> DetectTenantVolumeAsync(
        Guid tenantId,
        bool persist,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow.Date;
        var from = now.AddDays(-BaselineDays);
        var receipts = await _db.Receipts.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && r.IssuedAt >= from)
            .GroupBy(r => r.IssuedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var findings = new List<TseAnomaly>();
        if (receipts.Count < MinBaselineSamples)
            return findings;

        var today = receipts.FirstOrDefault(r => r.Day == now)?.Count ?? 0;
        var baselineDays = receipts.Where(r => r.Day < now).Select(r => (double)r.Count).ToList();
        if (baselineDays.Count < MinBaselineSamples)
            return findings;

        var expected = baselineDays.Average();
        MaybeAdd(
            findings,
            device: null,
            tenantId,
            TseAnomalyMetrics.DailyTransactionVolume,
            today,
            expected,
            higherIsWorse: null, // both spike and drop matter
            description: $"Daily signed receipt volume is {today} vs baseline average {expected:0.#}.",
            action: "Verify POS traffic, offline queue, and capacity planning alerts.");

        if (persist)
        {
            await PersistFindingsAsync(findings, actorUserId: null, cancellationToken)
                .ConfigureAwait(false);
        }

        return findings;
    }

    private async Task<(double Expected, double StdDev)?> ComputeTenantBaselineAsync(
        Guid tenantId,
        string metric,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var from = now.AddDays(-BaselineDays);

        if (string.Equals(metric, TseAnomalyMetrics.DailyTransactionVolume, StringComparison.OrdinalIgnoreCase))
        {
            var counts = await _db.Receipts.AsNoTracking().IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId && r.IssuedAt >= from && r.IssuedAt < now.Date)
                .GroupBy(r => r.IssuedAt.Date)
                .Select(g => g.Count())
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (counts.Count < MinBaselineSamples)
                return null;
            var values = counts.Select(c => (double)c).ToList();
            return (values.Average(), StdDev(values));
        }

        var samples = await _db.TseDeviceHealthSamples.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.CheckedAtUtc >= from && s.CheckedAtUtc < now.AddHours(-RecentHours))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (samples.Count < MinBaselineSamples)
            return null;

        List<double> series = metric switch
        {
            TseAnomalyMetrics.HealthScore => samples.Select(s => (double)s.HealthScore).ToList(),
            TseAnomalyMetrics.ResponseTimeMs => samples
                .Where(s => s.ResponseTimeMs.HasValue)
                .Select(s => (double)s.ResponseTimeMs!.Value)
                .ToList(),
            TseAnomalyMetrics.ErrorRatePercent =>
            [
                100.0 * samples.Count(s => s.HealthStatus != TseHealthStatus.Healthy)
                / Math.Max(1, samples.Count),
            ],
            _ => new List<double>(),
        };

        if (series.Count < 1)
            return null;
        return (series.Average(), StdDev(series));
    }

    private void MaybeAdd(
        List<TseAnomaly> findings,
        TseDevice device,
        string metric,
        double current,
        double expected,
        bool higherIsWorse,
        string description,
        string action)
    {
        if (device.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return;
        MaybeAdd(findings, device, tenantId, metric, current, expected, higherIsWorse, description, action);
    }

    private static void MaybeAdd(
        List<TseAnomaly> findings,
        TseDevice? device,
        Guid tenantId,
        string metric,
        double current,
        double expected,
        bool? higherIsWorse,
        string description,
        string action)
    {
        var deviation = PercentDeviation(current, expected);
        if (deviation < MinDeviationToPersist)
            return;

        // Directional filter: ignore "good" deviations when direction is known
        if (higherIsWorse == true && current <= expected)
            return;
        if (higherIsWorse == false && current >= expected)
            return;

        var severity = TseAnomalySeverities.FromDeviationPercent(deviation);
        findings.Add(new TseAnomaly
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeviceId = device?.Id,
            MetricName = metric,
            CurrentValue = Round(current),
            ExpectedValue = Round(expected),
            DeviationPercent = Round(deviation),
            Severity = severity,
            Description = Truncate(description, 1000)!,
            SuggestedAction = Truncate(action, 500),
            DetectedAt = DateTime.UtcNow,
            IsResolved = false,
        });
    }

    private async Task<List<TseAnomaly>> PersistFindingsAsync(
        List<TseAnomaly> findings,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        if (findings.Count == 0)
            return findings;

        var since = DateTime.UtcNow.AddHours(-DedupHours);
        var persisted = new List<TseAnomaly>();

        foreach (var finding in findings)
        {
            var exists = await _db.TseAnomalies.AsNoTracking()
                .AnyAsync(
                    a => a.TenantId == finding.TenantId
                         && a.MetricName == finding.MetricName
                         && a.DeviceId == finding.DeviceId
                         && !a.IsResolved
                         && a.DetectedAt >= since,
                    cancellationToken)
                .ConfigureAwait(false);
            if (exists)
            {
                // Return the open duplicate for the result payload
                var open = await _db.TseAnomalies.AsNoTracking()
                    .Where(a => a.TenantId == finding.TenantId
                                && a.MetricName == finding.MetricName
                                && a.DeviceId == finding.DeviceId
                                && !a.IsResolved)
                    .OrderByDescending(a => a.DetectedAt)
                    .FirstAsync(cancellationToken)
                    .ConfigureAwait(false);
                persisted.Add(open);
                continue;
            }

            _db.TseAnomalies.Add(finding);
            persisted.Add(finding);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _ = actorUserId; // reserved for future audit actor field
        return persisted;
    }

    private async Task PublishAlertAsync(
        Guid tenantId,
        TseAnomalyResultDto result,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _activity.TryPublishAsync(
                    tenantId,
                    ActivityEventType.TseAnomalyDetected,
                    new
                    {
                        TenantId = tenantId.ToString("D"),
                        OverallSeverity = result.OverallSeverity,
                        RequiresAction = result.RequiresAction,
                        Count = result.Anomalies.Count,
                        Summary = result.Summary,
                    },
                    actorUserId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                    dedupKey: $"tse-anomaly:{tenantId:N}:{DateTime.UtcNow:yyyyMMddHH}",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish TseAnomalyDetected for {TenantId}", tenantId);
        }
    }

    private static TseAnomalyResultDto BuildResult(
        Guid tenantId,
        Guid? deviceId,
        IReadOnlyList<TseAnomaly> anomalies)
    {
        var dtos = anomalies.Select(MapAnomaly).OrderByDescending(a => TseAnomalySeverities.Rank(a.Severity))
            .ThenByDescending(a => a.Deviation)
            .ToList();
        var overall = dtos.Aggregate(TseAnomalySeverities.Info, (acc, a) =>
            TseAnomalySeverities.Max(acc, a.Severity));
        var requires = TseAnomalySeverities.Rank(overall) >= TseAnomalySeverities.Rank(TseAnomalySeverities.High);

        return new TseAnomalyResultDto
        {
            TenantId = tenantId,
            DeviceId = deviceId,
            DetectedAt = DateTime.UtcNow,
            Anomalies = dtos,
            OverallSeverity = overall,
            RequiresAction = requires,
            Summary = dtos.Count == 0
                ? "No statistical anomalies detected for the lookback window."
                : $"Detected {dtos.Count} anomal{(dtos.Count == 1 ? "y" : "ies")}; overall severity {overall}.",
            DiagnosticOnly = true,
        };
    }

    private static TseAnomalyDto MapAnomaly(TseAnomaly a) => new()
    {
        Id = a.Id,
        TenantId = a.TenantId,
        DeviceId = a.DeviceId,
        MetricName = a.MetricName,
        CurrentValue = a.CurrentValue,
        ExpectedValue = a.ExpectedValue,
        Deviation = a.DeviationPercent,
        Severity = a.Severity,
        Description = a.Description,
        SuggestedAction = a.SuggestedAction,
        DetectedAt = a.DetectedAt,
        IsResolved = a.IsResolved,
        ResolvedAt = a.ResolvedAt,
    };

    private async Task<Tenant> RequireTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
        return tenant;
    }

    private static string DeviceLabel(TseDevice d) =>
        string.IsNullOrWhiteSpace(d.SerialNumber) ? d.Id.ToString("N")[..8] : d.SerialNumber;

    private static double PercentDeviation(double current, double expected)
    {
        var denom = Math.Max(Math.Abs(expected), 0.0001);
        return Math.Abs(current - expected) / denom * 100.0;
    }

    private static double StdDev(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
            return 0;
        var mean = values.Average();
        var variance = values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1);
        return Math.Sqrt(variance);
    }

    private static double Round(double v) => Math.Round(v, 2);

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
