using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Computes tenant TSE SLA from health-probe samples (uptime / latency) and fiscal receipt signatures (success rate).
/// </summary>
public sealed class TseSlaMonitorService : ITseSlaMonitorService
{
    private const int MaxPeriodDays = 366;
    private const int MinSamplesForUptime = 1;

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IActivityEventPublisher _activity;
    private readonly ILogger<TseSlaMonitorService> _logger;

    public TseSlaMonitorService(
        AppDbContext db,
        IOptionsMonitor<TseOptions> tseOptions,
        IActivityEventPublisher activity,
        ILogger<TseSlaMonitorService> logger)
    {
        _db = db;
        _tseOptions = tseOptions;
        _activity = activity;
        _logger = logger;
    }

    public async Task<TseSlaReportDto> GetSlaReportAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        fromUtc = NormalizeUtc(fromUtc);
        toUtc = NormalizeUtc(toUtc);
        if (toUtc <= fromUtc)
            throw new ArgumentException("toUtc must be strictly greater than fromUtc.", nameof(toUtc));
        if ((toUtc - fromUtc).TotalDays > MaxPeriodDays)
            throw new ArgumentException($"Period must be at most {MaxPeriodDays} days.", nameof(toUtc));

        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");

        var opts = _tseOptions.CurrentValue;
        var targetUptime = Clamp(opts.SlaTargetUptimePercent, 50, 100);
        var targetResponseMs = Math.Max(50, opts.SlaTargetResponseTimeMs);
        var targetSuccess = Clamp(opts.SlaTargetSuccessRatePercent, 50, 100);

        var retentionDays = Math.Clamp(opts.HealthSampleRetentionDays, 7, 90);
        var minFrom = DateTime.UtcNow.AddDays(-retentionDays);
        var sampleFrom = fromUtc < minFrom ? minFrom : fromUtc;

        var samples = await _db.TseDeviceHealthSamples.AsNoTracking()
            .Where(s => s.TenantId == tenantId
                        && s.CheckedAtUtc >= sampleFrom
                        && s.CheckedAtUtc < toUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var upCount = samples.Count(IsAvailableProbe);
        var uptime = samples.Count >= MinSamplesForUptime
            ? Round2(100.0 * upCount / samples.Count)
            : 100.0;

        var timed = samples
            .Where(s => s.ResponseTimeMs is > 0)
            .Select(s => (double)s.ResponseTimeMs!.Value)
            .ToList();
        var avgResponse = timed.Count > 0 ? Round2(timed.Average()) : 0;

        var (totalTx, signedTx) = await CountReceiptSignaturesAsync(tenantId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);
        var successRate = totalTx > 0 ? Round2(100.0 * signedTx / totalTx) : 100.0;

        var uptimeMet = samples.Count == 0 || uptime >= targetUptime;
        var responseMet = timed.Count == 0 || avgResponse <= targetResponseMs;
        var successMet = totalTx == 0 || successRate >= targetSuccess;

        var violations = BuildViolations(
            uptime,
            targetUptime,
            uptimeMet,
            avgResponse,
            targetResponseMs,
            responseMet,
            successRate,
            targetSuccess,
            successMet,
            samples.Count,
            timed.Count,
            totalTx);

        return new TseSlaReportDto
        {
            TenantId = tenantId,
            TenantName = tenant.Name,
            TenantSlug = tenant.Slug,
            PeriodStart = fromUtc,
            PeriodEnd = toUtc,
            UptimePercentage = uptime,
            TargetUptimePercentage = targetUptime,
            IsUptimeTargetMet = uptimeMet,
            AverageResponseTime = avgResponse,
            TargetResponseTime = targetResponseMs,
            IsResponseTimeTargetMet = responseMet,
            TotalTransactions = totalTx,
            SuccessfulTransactions = signedTx,
            SuccessRate = successRate,
            TargetSuccessRate = targetSuccess,
            IsSuccessRateTargetMet = successMet,
            HealthSampleCount = samples.Count,
            TimedSampleCount = timed.Count,
            Violations = violations,
            Grade = ComputeGrade(uptimeMet, responseMet, successMet, violations),
        };
    }

    public async Task<TseSlaStatusDto> GetCurrentSlaStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var lookbackHours = Math.Clamp(_tseOptions.CurrentValue.SlaStatusLookbackHours, 1, 168);
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddHours(-lookbackHours);
        var report = await GetSlaReportAsync(tenantId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);

        return new TseSlaStatusDto
        {
            TenantId = report.TenantId,
            TenantName = report.TenantName,
            AsOfUtc = toUtc,
            LookbackStartUtc = fromUtc,
            Grade = report.Grade,
            IsCompliant = report.Violations.Count == 0,
            UptimePercentage = report.UptimePercentage,
            AverageResponseTime = report.AverageResponseTime,
            SuccessRate = report.SuccessRate,
            OpenViolationCount = report.Violations.Count,
            Report = report,
        };
    }

    public async Task<TseSlaAlertDto> CheckSlaViolationsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        TseSlaReportDto report;
        try
        {
            var status = await GetCurrentSlaStatusAsync(tenantId, cancellationToken).ConfigureAwait(false);
            report = status.Report;
        }
        catch (KeyNotFoundException)
        {
            return new TseSlaAlertDto
            {
                TenantId = tenantId,
                HasViolations = false,
                Severity = "Info",
                Message = "Tenant not found.",
            };
        }

        if (report.Violations.Count == 0)
        {
            return new TseSlaAlertDto
            {
                TenantId = tenantId,
                HasViolations = false,
                Severity = "Info",
                Message = "TSE SLA targets met for the current lookback window.",
                Report = report,
            };
        }

        var severity = report.Violations.Any(v =>
            string.Equals(v.Severity, "Critical", StringComparison.OrdinalIgnoreCase))
            ? "Critical"
            : "Warning";

        var message =
            $"TSE SLA violations ({report.Violations.Count}): "
            + string.Join("; ", report.Violations.Select(v => v.Code));

        await _activity.TryPublishAsync(
                tenantId,
                ActivityEventType.TseSlaViolation,
                new
                {
                    TenantId = tenantId.ToString("D"),
                    report.Grade,
                    Severity = severity,
                    ViolationCount = report.Violations.Count,
                    Codes = report.Violations.Select(v => v.Code).ToArray(),
                    report.UptimePercentage,
                    report.AverageResponseTime,
                    report.SuccessRate,
                },
                actorUserId: "system",
                dedupKey: $"tse-sla-violation:{tenantId:N}:{DateTime.UtcNow:yyyyMMddHH}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        _logger.LogWarning(
            "TSE SLA violations TenantId={TenantId} Grade={Grade} Count={Count} Severity={Severity}",
            tenantId,
            report.Grade,
            report.Violations.Count,
            severity);

        return new TseSlaAlertDto
        {
            TenantId = tenantId,
            HasViolations = true,
            Severity = severity,
            Message = message,
            AlertPublished = true,
            Violations = report.Violations,
            Report = report,
        };
    }

    private async Task<(int Total, int Signed)> CountReceiptSignaturesAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Receipts.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId
                        && r.IssuedAt >= fromUtc
                        && r.IssuedAt < toUtc)
            .Select(r => r.SignatureValue)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = rows.Count;
        var signed = rows.Count(s => !string.IsNullOrWhiteSpace(s));
        return (total, signed);
    }

    private static List<TseSlaViolationDto> BuildViolations(
        double uptime,
        double targetUptime,
        bool uptimeMet,
        double avgResponse,
        double targetResponseMs,
        bool responseMet,
        double successRate,
        double targetSuccess,
        bool successMet,
        int sampleCount,
        int timedCount,
        int totalTx)
    {
        var now = DateTime.UtcNow;
        var list = new List<TseSlaViolationDto>();

        if (!uptimeMet && sampleCount > 0)
        {
            list.Add(new TseSlaViolationDto
            {
                Code = TseSlaViolationCodes.Uptime,
                Metric = "Uptime",
                Severity = uptime < targetUptime - 5 ? "Critical" : "Warning",
                Message =
                    $"Uptime {uptime:0.##}% is below target {targetUptime:0.##}% "
                    + $"({sampleCount} health samples).",
                ActualValue = uptime,
                TargetValue = targetUptime,
                DetectedAt = now,
            });
        }

        if (!responseMet && timedCount > 0)
        {
            list.Add(new TseSlaViolationDto
            {
                Code = TseSlaViolationCodes.ResponseTime,
                Metric = "ResponseTime",
                Severity = avgResponse >= targetResponseMs * 2 ? "Critical" : "Warning",
                Message =
                    $"Average probe response {avgResponse:0.##} ms exceeds target {targetResponseMs:0.##} ms.",
                ActualValue = avgResponse,
                TargetValue = targetResponseMs,
                DetectedAt = now,
            });
        }

        if (!successMet && totalTx > 0)
        {
            list.Add(new TseSlaViolationDto
            {
                Code = TseSlaViolationCodes.SuccessRate,
                Metric = "SuccessRate",
                Severity = successRate < targetSuccess - 5 ? "Critical" : "Warning",
                Message =
                    $"Signed receipt success rate {successRate:0.##}% is below target {targetSuccess:0.##}% "
                    + $"({totalTx} receipts).",
                ActualValue = successRate,
                TargetValue = targetSuccess,
                DetectedAt = now,
            });
        }

        return list;
    }

    private static string ComputeGrade(
        bool uptimeMet,
        bool responseMet,
        bool successMet,
        IReadOnlyList<TseSlaViolationDto> violations)
    {
        var met = (uptimeMet ? 1 : 0) + (responseMet ? 1 : 0) + (successMet ? 1 : 0);
        if (met == 3 && violations.Count == 0)
            return TseSlaGrades.A;
        if (met == 2)
            return TseSlaGrades.B;
        if (met == 1)
            return TseSlaGrades.C;
        if (met == 0 && violations.Any(v =>
                string.Equals(v.Severity, "Critical", StringComparison.OrdinalIgnoreCase)))
            return TseSlaGrades.F;
        if (met == 0)
            return TseSlaGrades.D;
        return TseSlaGrades.B;
    }

    private static bool IsAvailableProbe(TseDeviceHealthSample sample) =>
        sample.HealthStatus is TseHealthStatus.Healthy or TseHealthStatus.Degraded;

    private static double Clamp(double value, double min, double max) =>
        Math.Min(max, Math.Max(min, value));

    private static double Round2(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
