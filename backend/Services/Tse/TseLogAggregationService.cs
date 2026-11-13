using System.Text.RegularExpressions;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Aggregates TSE operational signals from activity feed, failover logs, incident logs, and health samples.
/// </summary>
public sealed class TseLogAggregationService : ITseLogAggregationService
{
    private const int MaxPeriodDays = 90;
    private const int MaxCollect = 2000;
    private const int MaxSearchTake = 500;

    private static readonly HashSet<ActivityEventType> TseActivityTypes = new()
    {
        ActivityEventType.TseFailoverActivated,
        ActivityEventType.TseFailoverNoBackup,
        ActivityEventType.TseFailoverReverted,
        ActivityEventType.TseFailoverStarted,
        ActivityEventType.TseFailoverFailed,
        ActivityEventType.TseFailoverBackupLowHealth,
        ActivityEventType.TseCertificateExpiringSoon,
        ActivityEventType.TseCertificateExpired,
        ActivityEventType.TseCertificateRenewed,
        ActivityEventType.TseCertificateRenewalScheduled,
        ActivityEventType.TsePerformanceSlow,
        ActivityEventType.TsePerformanceHighErrorRate,
        ActivityEventType.TseCostAnomaly,
        ActivityEventType.TsePredictiveFailureRisk,
        ActivityEventType.TseIncidentCreated,
        ActivityEventType.TseIncidentResolved,
        ActivityEventType.TseSlaViolation,
        ActivityEventType.TseCapacityNearLimit,
        ActivityEventType.TseDrDrillCompleted,
    };

    private readonly AppDbContext _db;
    private readonly ILogger<TseLogAggregationService> _logger;

    public TseLogAggregationService(AppDbContext db, ILogger<TseLogAggregationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TseLogAggregationResultDto> AggregateLogsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        (fromUtc, toUtc) = NormalizePeriod(fromUtc, toUtc);
        var tenant = await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var logs = await CollectLogsAsync(tenantId, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);

        var patterns = BuildPatterns(logs);
        var anomalies = DetectAnomalies(logs, fromUtc, toUtc);

        return new TseLogAggregationResultDto
        {
            TenantId = tenantId,
            TenantName = tenant.Name,
            PeriodStart = fromUtc,
            PeriodEnd = toUtc,
            TotalLogs = logs.Count,
            ErrorLogs = logs.Count(l => l.Level == TseLogLevels.Error),
            WarningLogs = logs.Count(l => l.Level == TseLogLevels.Warning),
            InfoLogs = logs.Count(l => l.Level == TseLogLevels.Info),
            LogsByProvider = logs
                .GroupBy(l => string.IsNullOrWhiteSpace(l.Provider) ? "unknown" : l.Provider!)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase),
            LogsByDevice = logs
                .Where(l => l.DeviceId is not null)
                .GroupBy(l => l.DeviceLabel ?? l.DeviceId!.Value.ToString("D"))
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase),
            LogsBySource = logs
                .GroupBy(l => l.Source)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase),
            Patterns = patterns,
            Anomalies = anomalies,
            RecentLogs = logs.OrderByDescending(l => l.Timestamp).Take(50).ToList(),
        };
    }

    public async Task<TseLogSearchResultDto> SearchLogsAsync(
        TseLogSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(request));

        var toUtc = NormalizeUtc(request.ToUtc ?? DateTime.UtcNow);
        var fromUtc = NormalizeUtc(request.FromUtc ?? toUtc.AddDays(-7));
        (fromUtc, toUtc) = NormalizePeriod(fromUtc, toUtc);

        await RequireTenantAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
        var logs = await CollectLogsAsync(request.TenantId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<TseLogEntryDto> filtered = logs;

        if (!string.IsNullOrWhiteSpace(request.Level)
            && !string.Equals(request.Level, "All", StringComparison.OrdinalIgnoreCase))
        {
            var level = NormalizeLevel(request.Level);
            filtered = filtered.Where(l => string.Equals(l.Level, level, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            var provider = request.Provider.Trim();
            filtered = filtered.Where(l =>
                string.Equals(l.Provider, provider, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            var source = request.Source.Trim();
            filtered = filtered.Where(l =>
                string.Equals(l.Source, source, StringComparison.OrdinalIgnoreCase));
        }

        if (request.DeviceId is { } deviceId && deviceId != Guid.Empty)
            filtered = filtered.Where(l => l.DeviceId == deviceId);

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var q = request.Query.Trim();
            filtered = filtered.Where(l =>
                l.Message.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (l.Category?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (l.DeviceLabel?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var matched = filtered.OrderByDescending(l => l.Timestamp).ToList();
        var skip = Math.Max(0, request.Skip);
        var take = Math.Clamp(request.Take <= 0 ? 100 : request.Take, 1, MaxSearchTake);

        return new TseLogSearchResultDto
        {
            TenantId = request.TenantId,
            TotalMatched = matched.Count,
            Skip = skip,
            Take = take,
            Logs = matched.Skip(skip).Take(take).ToList(),
        };
    }

    public async Task<TseLogAnalysisReportDto> AnalyzeLogsAsync(
        Guid tenantId,
        TseLogAnalysisRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var toUtc = NormalizeUtc(request.ToUtc ?? DateTime.UtcNow);
        var fromUtc = NormalizeUtc(request.FromUtc ?? toUtc.AddDays(-7));
        var aggregation = await AggregateLogsAsync(tenantId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);

        var focus = (request.FocusLevel ?? "All").Trim();
        var patterns = aggregation.Patterns;
        if (!string.Equals(focus, "All", StringComparison.OrdinalIgnoreCase))
        {
            var level = NormalizeLevel(focus);
            patterns = patterns.Where(p => string.Equals(p.Level, level, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var errorRate = aggregation.TotalLogs == 0
            ? 0
            : Round2(100.0 * aggregation.ErrorLogs / aggregation.TotalLogs);
        var warningRate = aggregation.TotalLogs == 0
            ? 0
            : Round2(100.0 * aggregation.WarningLogs / aggregation.TotalLogs);

        var recommendations = new List<string>();
        if (errorRate >= 20)
            recommendations.Add("Error rate is elevated — review failover and health probe failures first.");
        if (aggregation.Anomalies.Count > 0)
            recommendations.Add("Investigate detected log anomalies in the selected window.");
        if (patterns.Any(p => p.Level == TseLogLevels.Error && p.Count >= 5))
            recommendations.Add("Repeated error patterns suggest a systemic device or provider issue.");
        if (recommendations.Count == 0)
            recommendations.Add("No critical log analysis findings for this window.");

        var summary =
            $"Analyzed {aggregation.TotalLogs} TSE operational log entries "
            + $"({aggregation.ErrorLogs} errors, {aggregation.WarningLogs} warnings). "
            + $"Error rate {errorRate:0.##}%.";

        _logger.LogInformation(
            "TSE log analysis TenantId={TenantId} Total={Total} Errors={Errors}",
            tenantId,
            aggregation.TotalLogs,
            aggregation.ErrorLogs);

        return new TseLogAnalysisReportDto
        {
            TenantId = tenantId,
            PeriodStart = aggregation.PeriodStart,
            PeriodEnd = aggregation.PeriodEnd,
            GeneratedAt = DateTime.UtcNow,
            Summary = summary,
            ErrorRatePercent = errorRate,
            WarningRatePercent = warningRate,
            TopPatterns = patterns.Take(10).ToList(),
            Anomalies = aggregation.Anomalies,
            Recommendations = recommendations,
            Aggregation = aggregation,
        };
    }

    private async Task<List<TseLogEntryDto>> CollectLogsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var devices = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId)
            .Select(d => new { d.Id, d.DeviceId, d.SerialNumber, d.Provider, d.DeviceType })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var deviceMap = devices.ToDictionary(
            d => d.Id,
            d => (
                Label: string.IsNullOrWhiteSpace(d.DeviceId) ? d.SerialNumber : d.DeviceId,
                Provider: string.IsNullOrWhiteSpace(d.Provider) ? d.DeviceType : d.Provider!));

        string Label(Guid? id) =>
            id is { } g && deviceMap.TryGetValue(g, out var info) ? info.Label : id?.ToString("D") ?? "unknown";

        string? ProviderOf(Guid? id) =>
            id is { } g && deviceMap.TryGetValue(g, out var info) ? info.Provider : null;

        var logs = new List<TseLogEntryDto>(capacity: 256);

        var activities = await _db.ActivityEvents.AsNoTracking().IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId
                        && a.CreatedAtUtc >= fromUtc
                        && a.CreatedAtUtc < toUtc
                        && TseActivityTypes.Contains(a.Type))
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(MaxCollect)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var a in activities)
        {
            Guid? deviceId = null;
            if (Guid.TryParse(a.EntityId, out var parsed) && a.EntityType == "TseDevice")
                deviceId = parsed;

            logs.Add(new TseLogEntryDto
            {
                Id = a.Id,
                TenantId = tenantId,
                Timestamp = a.CreatedAtUtc,
                Level = MapActivitySeverity(a.Severity),
                Source = TseLogSources.Activity,
                Message = string.IsNullOrWhiteSpace(a.Description)
                    ? a.Title
                    : $"{a.Title}: {a.Description}",
                DeviceId = deviceId,
                DeviceLabel = Label(deviceId),
                Provider = ProviderOf(deviceId),
                Category = a.Type.ToString(),
                Metadata = new Dictionary<string, string>
                {
                    ["type"] = a.Type.ToString(),
                    ["severity"] = a.Severity,
                },
            });
        }

        var failovers = await _db.TseFailoverLogs.AsNoTracking().IgnoreQueryFilters()
            .Where(f => f.TenantId == tenantId
                        && f.StartedAt >= fromUtc
                        && f.StartedAt < toUtc)
            .OrderByDescending(f => f.StartedAt)
            .Take(MaxCollect)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var f in failovers)
        {
            var msg = f.IsSuccessful
                ? $"Failover {f.FailoverType}/{f.TriggerReason} succeeded."
                : $"Failover {f.FailoverType}/{f.TriggerReason} failed: {f.ErrorMessage ?? "unknown error"}";
            if (!string.IsNullOrWhiteSpace(f.Notes))
                msg += $" Notes: {f.Notes}";

            logs.Add(new TseLogEntryDto
            {
                Id = f.Id,
                TenantId = tenantId,
                Timestamp = f.StartedAt,
                Level = f.IsSuccessful ? TseLogLevels.Warning : TseLogLevels.Error,
                Source = TseLogSources.Failover,
                Message = msg,
                DeviceId = f.PrimaryDeviceId,
                DeviceLabel = Label(f.PrimaryDeviceId),
                Provider = ProviderOf(f.PrimaryDeviceId),
                Category = f.TriggerReason,
                Metadata = new Dictionary<string, string>
                {
                    ["failoverType"] = f.FailoverType,
                    ["isSuccessful"] = f.IsSuccessful.ToString(),
                    ["backupDeviceId"] = f.BackupDeviceId?.ToString("D") ?? string.Empty,
                },
            });
        }

        var incidentLogs = await (
                from log in _db.TseIncidentLogs.AsNoTracking()
                join incident in _db.TseIncidents.AsNoTracking() on log.IncidentId equals incident.Id
                where incident.TenantId == tenantId
                      && log.CreatedAt >= fromUtc
                      && log.CreatedAt < toUtc
                orderby log.CreatedAt descending
                select new { log, incident.DeviceId, incident.Severity, incident.Title })
            .Take(MaxCollect)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in incidentLogs)
        {
            logs.Add(new TseLogEntryDto
            {
                Id = row.log.Id,
                TenantId = tenantId,
                Timestamp = row.log.CreatedAt,
                Level = MapIncidentSeverity(row.Severity),
                Source = TseLogSources.Incident,
                Message = $"[{row.Title}] {row.log.EventType}: {row.log.Message}",
                DeviceId = row.DeviceId,
                DeviceLabel = Label(row.DeviceId),
                Provider = ProviderOf(row.DeviceId),
                Category = row.log.EventType,
                Metadata = new Dictionary<string, string>
                {
                    ["incidentSeverity"] = row.Severity,
                    ["eventType"] = row.log.EventType,
                },
            });
        }

        var samples = await _db.TseDeviceHealthSamples.AsNoTracking()
            .Where(s => s.TenantId == tenantId
                        && s.CheckedAtUtc >= fromUtc
                        && s.CheckedAtUtc < toUtc
                        && (s.HealthStatus == TseHealthStatus.Unhealthy
                            || s.HealthStatus == TseHealthStatus.Offline
                            || s.HealthStatus == TseHealthStatus.Expired
                            || s.HealthStatus == TseHealthStatus.Revoked
                            || s.HealthStatus == TseHealthStatus.Degraded
                            || (s.ResponseTimeMs != null && s.ResponseTimeMs >= 3000)))
            .OrderByDescending(s => s.CheckedAtUtc)
            .Take(MaxCollect)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var s in samples)
        {
            logs.Add(new TseLogEntryDto
            {
                Id = s.Id,
                TenantId = tenantId,
                Timestamp = s.CheckedAtUtc,
                Level = MapHealthStatus(s.HealthStatus, s.ResponseTimeMs),
                Source = TseLogSources.HealthSample,
                Message = string.IsNullOrWhiteSpace(s.Message)
                    ? $"Health probe status={s.HealthStatus}, score={s.HealthScore}"
                      + (s.ResponseTimeMs is { } ms ? $", response={ms}ms" : string.Empty)
                    : s.Message!,
                DeviceId = s.DeviceId,
                DeviceLabel = Label(s.DeviceId),
                Provider = ProviderOf(s.DeviceId),
                Category = s.HealthStatus.ToString(),
                Metadata = new Dictionary<string, string>
                {
                    ["healthScore"] = s.HealthScore.ToString(),
                    ["healthStatus"] = s.HealthStatus.ToString(),
                    ["responseTimeMs"] = s.ResponseTimeMs?.ToString() ?? string.Empty,
                },
            });
        }

        return logs
            .OrderByDescending(l => l.Timestamp)
            .Take(MaxCollect)
            .ToList();
    }

    private static IReadOnlyList<TseLogPatternDto> BuildPatterns(IReadOnlyList<TseLogEntryDto> logs)
    {
        return logs
            .GroupBy(l => NormalizePatternKey(l.Message))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Select(g => new TseLogPatternDto
            {
                Pattern = g.Key,
                Count = g.Count(),
                Level = DominantLevel(g.Select(x => x.Level)),
                SampleMessage = g.First().Message,
            })
            .OrderByDescending(p => p.Count)
            .Take(20)
            .ToList();
    }

    private static IReadOnlyList<TseLogAnomalyDto> DetectAnomalies(
        IReadOnlyList<TseLogEntryDto> logs,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var anomalies = new List<TseLogAnomalyDto>();
        if (logs.Count < 5)
            return anomalies;

        var errors = logs.Where(l => l.Level == TseLogLevels.Error).ToList();
        var errorRate = 100.0 * errors.Count / logs.Count;
        if (errorRate >= 25)
        {
            anomalies.Add(new TseLogAnomalyDto
            {
                Code = "high_error_rate",
                Severity = errorRate >= 50 ? "Critical" : "Warning",
                Message = $"Error rate {errorRate:0.#}% across {logs.Count} log entries.",
                DetectedAt = DateTime.UtcNow,
                Score = Round2(errorRate),
            });
        }

        // Burst: many errors in any 1-hour window.
        var burst = errors
            .GroupBy(e => new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour, 0, 0, DateTimeKind.Utc))
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .FirstOrDefault();

        if (burst is { Count: >= 5 })
        {
            anomalies.Add(new TseLogAnomalyDto
            {
                Code = "error_burst",
                Severity = burst.Count >= 10 ? "Critical" : "Warning",
                Message = $"Error burst: {burst.Count} errors around {burst.Hour:u}.",
                DetectedAt = burst.Hour,
                Score = burst.Count,
            });
        }

        var failoverFails = logs.Count(l =>
            l.Source == TseLogSources.Failover && l.Level == TseLogLevels.Error);
        if (failoverFails >= 2)
        {
            anomalies.Add(new TseLogAnomalyDto
            {
                Code = "failover_failures",
                Severity = "Critical",
                Message = $"{failoverFails} failed failover events in the analysis window.",
                DetectedAt = DateTime.UtcNow,
                Score = failoverFails,
            });
        }

        var hours = Math.Max(1.0, (toUtc - fromUtc).TotalHours);
        var density = logs.Count / hours;
        if (density >= 40)
        {
            anomalies.Add(new TseLogAnomalyDto
            {
                Code = "high_log_volume",
                Severity = "Warning",
                Message = $"High log density ≈ {density:0.#}/hour.",
                DetectedAt = DateTime.UtcNow,
                Score = Round2(density),
            });
        }

        return anomalies;
    }

    private async Task<Tenant> RequireTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
        return tenant;
    }

    private static (DateTime From, DateTime To) NormalizePeriod(DateTime fromUtc, DateTime toUtc)
    {
        fromUtc = NormalizeUtc(fromUtc);
        toUtc = NormalizeUtc(toUtc);
        if (toUtc <= fromUtc)
            throw new ArgumentException("toUtc must be strictly greater than fromUtc.");
        if ((toUtc - fromUtc).TotalDays > MaxPeriodDays)
            throw new ArgumentException($"Period must be at most {MaxPeriodDays} days.");
        return (fromUtc, toUtc);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private static string MapActivitySeverity(string severity) =>
        ActivitySeverityNames.NormalizeOrDefault(severity) switch
        {
            ActivitySeverityNames.Critical or ActivitySeverityNames.Error => TseLogLevels.Error,
            ActivitySeverityNames.Warning => TseLogLevels.Warning,
            _ => TseLogLevels.Info,
        };

    private static string MapIncidentSeverity(string severity) =>
        severity.Trim().ToLowerInvariant() switch
        {
            "critical" or "high" => TseLogLevels.Error,
            "medium" => TseLogLevels.Warning,
            _ => TseLogLevels.Info,
        };

    private static string MapHealthStatus(TseHealthStatus status, int? responseMs) =>
        status switch
        {
            TseHealthStatus.Offline or TseHealthStatus.Expired or TseHealthStatus.Revoked
                or TseHealthStatus.Unhealthy => TseLogLevels.Error,
            TseHealthStatus.Degraded => TseLogLevels.Warning,
            _ when responseMs is >= 3000 => TseLogLevels.Warning,
            _ => TseLogLevels.Info,
        };

    private static string NormalizeLevel(string? level)
    {
        var raw = (level ?? TseLogLevels.Info).Trim();
        if (raw.Equals("Error", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("Critical", StringComparison.OrdinalIgnoreCase))
            return TseLogLevels.Error;
        if (raw.Equals("Warning", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("Warn", StringComparison.OrdinalIgnoreCase))
            return TseLogLevels.Warning;
        return TseLogLevels.Info;
    }

    private static string NormalizePatternKey(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;
        var trimmed = message.Trim();
        if (trimmed.Length > 120)
            trimmed = trimmed[..120];
        // Collapse GUIDs and long numbers for pattern grouping.
        trimmed = Regex.Replace(trimmed, @"[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}", "{id}");
        trimmed = Regex.Replace(trimmed, @"\b\d{3,}\b", "{n}");
        return trimmed;
    }

    private static string DominantLevel(IEnumerable<string> levels)
    {
        var list = levels.ToList();
        if (list.Any(l => l == TseLogLevels.Error))
            return TseLogLevels.Error;
        if (list.Any(l => l == TseLogLevels.Warning))
            return TseLogLevels.Warning;
        return TseLogLevels.Info;
    }

    private static double Round2(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
