using System.Globalization;
using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Aggregates TSE receipts, devices, health samples, and activity alerts into BI dashboards.
/// Diagnostic analytics only — not DEP / Finanzamt proof.
/// </summary>
public sealed class TseReportingService : ITseReportingService
{
    private const int MinLookback = 7;
    private const int MaxLookback = 90;

    private static readonly ActivityEventType[] TseAlertTypes =
    {
        ActivityEventType.TsePerformanceSlow,
        ActivityEventType.TsePerformanceHighErrorRate,
        ActivityEventType.TseCostAnomaly,
        ActivityEventType.TsePredictiveFailureRisk,
        ActivityEventType.TseIncidentCreated,
        ActivityEventType.TseSlaViolation,
        ActivityEventType.TseCapacityNearLimit,
        ActivityEventType.TseAnomalyDetected,
        ActivityEventType.TseFailoverActivated,
        ActivityEventType.TseCertificateExpiringSoon,
    };

    private readonly AppDbContext _db;
    private readonly ILogger<TseReportingService> _logger;

    public TseReportingService(AppDbContext db, ILogger<TseReportingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TseBiDashboardDto> GetDashboardDataAsync(
        Guid tenantId,
        int lookbackDays = 30,
        CancellationToken cancellationToken = default)
    {
        var tenant = await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        lookbackDays = Math.Clamp(lookbackDays, MinLookback, MaxLookback);
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.Date.AddDays(-(lookbackDays - 1));

        var devices = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeDevices = devices.Count(d => d.IsActive);
        var overallHealth = activeDevices == 0
            ? 0
            : Math.Round(devices.Where(d => d.IsActive).Average(d => (double)d.HealthScore), 2);

        var receiptDates = await _db.Receipts.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && r.IssuedAt >= fromUtc && r.IssuedAt < toUtc)
            .Select(r => r.IssuedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var txnByDay = receiptDates
            .GroupBy(d => d.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var transactionTrends = new List<TseBiDailyTrendDto>();
        for (var d = fromUtc.Date; d <= toUtc.Date; d = d.AddDays(1))
        {
            txnByDay.TryGetValue(d, out var count);
            transactionTrends.Add(new TseBiDailyTrendDto
            {
                Date = d,
                Value = count,
                Label = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            });
        }

        var samples = await _db.TseDeviceHealthSamples.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.CheckedAtUtc >= fromUtc)
            .Select(s => new { s.CheckedAtUtc, s.HealthScore })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var healthByDay = samples
            .GroupBy(s => s.CheckedAtUtc.Date)
            .ToDictionary(g => g.Key, g => g.Average(x => (double)x.HealthScore));

        var healthTrends = new List<TseBiDailyTrendDto>();
        for (var d = fromUtc.Date; d <= toUtc.Date; d = d.AddDays(1))
        {
            healthByDay.TryGetValue(d, out var avg);
            healthTrends.Add(new TseBiDailyTrendDto
            {
                Date = d,
                Value = Math.Round(avg, 2),
                Label = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            });
        }

        var statusBreakdown = devices
            .Where(d => d.IsActive)
            .GroupBy(d => d.HealthStatus.ToString())
            .Select(g => new TseBiNamedCountDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var providerBreakdown = devices
            .Where(d => d.IsActive)
            .GroupBy(d => string.IsNullOrWhiteSpace(d.Provider) ? "unknown" : d.Provider.Trim().ToLowerInvariant())
            .Select(g => new TseBiNamedCountDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var alertFrom = toUtc.AddDays(-lookbackDays);
        var alertRows = await _db.ActivityEvents.AsNoTracking()
            .Where(e => e.TenantId == tenantId
                        && e.CreatedAtUtc >= alertFrom
                        && TseAlertTypes.Contains(e.Type))
            .Select(e => e.Severity)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Also count open statistical anomalies
        var openAnomalies = await _db.TseAnomalies.AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsResolved && a.DetectedAt >= alertFrom)
            .Select(a => a.Severity)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var critical = alertRows.Count(s =>
            string.Equals(s, ActivitySeverityNames.Critical, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, ActivitySeverityNames.Error, StringComparison.OrdinalIgnoreCase));
        var warning = alertRows.Count(s =>
            string.Equals(s, ActivitySeverityNames.Warning, StringComparison.OrdinalIgnoreCase));
        var info = alertRows.Count - critical - warning;
        if (info < 0) info = 0;

        critical += openAnomalies.Count(s =>
            string.Equals(s, TseAnomalySeverities.Critical, StringComparison.OrdinalIgnoreCase));
        warning += openAnomalies.Count(s =>
            string.Equals(s, TseAnomalySeverities.High, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, TseAnomalySeverities.Medium, StringComparison.OrdinalIgnoreCase));
        info += openAnomalies.Count(s =>
            string.Equals(s, TseAnomalySeverities.Low, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, TseAnomalySeverities.Info, StringComparison.OrdinalIgnoreCase));

        return new TseBiDashboardDto
        {
            TenantId = tenantId,
            TenantName = tenant.Name,
            GeneratedAt = toUtc,
            LookbackDays = lookbackDays,
            TotalTransactions = receiptDates.Count,
            ActiveDevices = activeDevices,
            TotalDevices = devices.Count,
            OverallHealthScore = overallHealth,
            TransactionTrends = transactionTrends,
            HealthTrends = healthTrends,
            StatusBreakdown = statusBreakdown,
            ProviderBreakdown = providerBreakdown,
            CriticalAlerts = critical,
            WarningAlerts = warning,
            InfoAlerts = Math.Max(0, info),
            DiagnosticOnly = true,
        };
    }

    public async Task<TseBiReportDto> GenerateReportAsync(
        TseBiReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(request));

        var lookback = request.LookbackDays;
        if (request.FromUtc.HasValue && request.ToUtc.HasValue)
        {
            var span = (request.ToUtc.Value - request.FromUtc.Value).TotalDays;
            lookback = (int)Math.Clamp(Math.Ceiling(span), MinLookback, MaxLookback);
        }

        var dashboard = await GetDashboardDataAsync(request.TenantId, lookback, cancellationToken)
            .ConfigureAwait(false);

        var toUtc = request.ToUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var fromUtc = request.FromUtc?.ToUniversalTime()
                      ?? toUtc.Date.AddDays(-(dashboard.LookbackDays - 1));

        return new TseBiReportDto
        {
            TenantId = request.TenantId,
            TenantName = dashboard.TenantName,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            GeneratedAt = DateTime.UtcNow,
            Dashboard = dashboard,
            Summary =
                $"{dashboard.TotalTransactions} transactions, {dashboard.ActiveDevices}/{dashboard.TotalDevices} active devices, " +
                $"health {dashboard.OverallHealthScore:0.#}%, alerts C/W/I = {dashboard.CriticalAlerts}/{dashboard.WarningAlerts}/{dashboard.InfoAlerts}.",
            DiagnosticOnly = true,
        };
    }

    public async Task<TseBiExportResultDto> ExportReportAsync(
        TseBiExportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(request));

        var format = string.IsNullOrWhiteSpace(request.Format)
            ? "csv"
            : request.Format.Trim().ToLowerInvariant();

        var report = await GenerateReportAsync(
                new TseBiReportRequestDto
                {
                    TenantId = request.TenantId,
                    LookbackDays = request.LookbackDays,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return format switch
        {
            "pdf" => BuildPdfExport(report),
            "csv" => BuildCsvExport(report),
            _ => throw new ArgumentException("Format must be csv or pdf."),
        };
    }

    private static TseBiExportResultDto BuildCsvExport(TseBiReportDto report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("section,key,value");
        sb.AppendLine(Csv("overview", "tenantId", report.TenantId.ToString("D")));
        sb.AppendLine(Csv("overview", "tenantName", report.TenantName ?? ""));
        sb.AppendLine(Csv("overview", "summary", report.Summary));
        sb.AppendLine(Csv("overview", "totalTransactions", report.Dashboard.TotalTransactions.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("overview", "activeDevices", report.Dashboard.ActiveDevices.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("overview", "totalDevices", report.Dashboard.TotalDevices.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("overview", "overallHealthScore", report.Dashboard.OverallHealthScore.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("overview", "criticalAlerts", report.Dashboard.CriticalAlerts.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("overview", "warningAlerts", report.Dashboard.WarningAlerts.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("overview", "infoAlerts", report.Dashboard.InfoAlerts.ToString(CultureInfo.InvariantCulture)));

        foreach (var t in report.Dashboard.TransactionTrends)
            sb.AppendLine(Csv("transactionTrend", t.Label, t.Value.ToString(CultureInfo.InvariantCulture)));
        foreach (var t in report.Dashboard.HealthTrends)
            sb.AppendLine(Csv("healthTrend", t.Label, t.Value.ToString(CultureInfo.InvariantCulture)));
        foreach (var s in report.Dashboard.StatusBreakdown)
            sb.AppendLine(Csv("statusBreakdown", s.Name, s.Count.ToString(CultureInfo.InvariantCulture)));
        foreach (var p in report.Dashboard.ProviderBreakdown)
            sb.AppendLine(Csv("providerBreakdown", p.Name, p.Count.ToString(CultureInfo.InvariantCulture)));

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return new TseBiExportResultDto
        {
            FileName = $"tse-bi-{report.TenantId:N}-{stamp}.csv",
            ContentType = "text/csv; charset=utf-8",
            ContentBase64 = Convert.ToBase64String(bytes),
            ByteLength = bytes.LongLength,
            DiagnosticOnly = true,
        };
    }

    private TseBiExportResultDto BuildPdfExport(TseBiReportDto report)
    {
        try
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var bytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Header().Text($"TSE Analytics — {report.TenantName ?? report.TenantId.ToString("D")}")
                        .SemiBold().FontSize(16);
                    page.Content().Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text($"Generated (UTC): {report.GeneratedAt:yyyy-MM-dd HH:mm}");
                        col.Item().Text(report.Summary);
                        col.Item().Text($"Transactions: {report.Dashboard.TotalTransactions}");
                        col.Item().Text(
                            $"Devices: {report.Dashboard.ActiveDevices}/{report.Dashboard.TotalDevices} active");
                        col.Item().Text($"Health score: {report.Dashboard.OverallHealthScore:0.##}%");
                        col.Item().Text(
                            $"Alerts — Critical: {report.Dashboard.CriticalAlerts}, Warning: {report.Dashboard.WarningAlerts}, Info: {report.Dashboard.InfoAlerts}");
                        col.Item().PaddingTop(12).Text("Provider breakdown").SemiBold();
                        foreach (var p in report.Dashboard.ProviderBreakdown)
                            col.Item().Text($"  {p.Name}: {p.Count}");
                        col.Item().PaddingTop(12).Text("Status breakdown").SemiBold();
                        foreach (var s in report.Dashboard.StatusBreakdown)
                            col.Item().Text($"  {s.Name}: {s.Count}");
                        col.Item().PaddingTop(16).Text("Diagnostic only — not a DEP / Finanzamt export.")
                            .FontColor(Colors.Grey.Darken1).FontSize(9);
                    });
                });
            }).GeneratePdf();

            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            return new TseBiExportResultDto
            {
                FileName = $"tse-bi-{report.TenantId:N}-{stamp}.pdf",
                ContentType = "application/pdf",
                ContentBase64 = Convert.ToBase64String(bytes),
                ByteLength = bytes.LongLength,
                DiagnosticOnly = true,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TSE BI PDF export failed; falling back to CSV");
            return BuildCsvExport(report);
        }
    }

    private static string Csv(string section, string key, string value)
    {
        static string Esc(string s) =>
            "\"" + (s ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return $"{Esc(section)},{Esc(key)},{Esc(value)}";
    }

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
}
