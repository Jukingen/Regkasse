using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Tenant TSE compliance report: receipt signature coverage + chain continuity + device health.
/// Reuses <see cref="IRksvComplianceReportService"/>, <see cref="IComplianceOperationalReportingService"/>,
/// and <see cref="ITseHealthTrendService"/> — does not reimplement fiscal signing.
/// </summary>
public sealed class TseComplianceReportService : ITseComplianceReportService
{
    private const int DefaultStatusLookbackDays = 7;
    private const int MaxPeriodDays = 366;

    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IRksvComplianceReportService _rksvCompliance;
    private readonly IComplianceOperationalReportingService _operational;
    private readonly ITseHealthTrendService _healthTrend;
    private readonly ILogger<TseComplianceReportService> _logger;

    public TseComplianceReportService(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        IRksvComplianceReportService rksvCompliance,
        IComplianceOperationalReportingService operational,
        ITseHealthTrendService healthTrend,
        ILogger<TseComplianceReportService> logger)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _rksvCompliance = rksvCompliance;
        _operational = operational;
        _healthTrend = healthTrend;
        _logger = logger;
    }

    public async Task<TseComplianceReportDto> GenerateComplianceReportAsync(
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

        var generatedAt = DateTime.UtcNow;
        var previousTenant = _tenantAccessor.TenantId;
        var previousSlug = _tenantAccessor.TenantSlug;

        RksvComplianceReportDto rksv;
        Models.Reports.TseChainContinuityReportDto chain;
        try
        {
            _tenantAccessor.TenantId = tenantId;
            _tenantAccessor.TenantSlug = tenant.Slug;

            rksv = await _rksvCompliance
                .BuildReportAsync(cashRegisterId: null, fromUtc, toUtc, cancellationToken)
                .ConfigureAwait(false);

            chain = await _operational
                .GetTseChainContinuityAsync(fromUtc, toUtc, cashRegisterId: null, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _tenantAccessor.TenantId = previousTenant;
            _tenantAccessor.TenantSlug = previousSlug;
        }

        var health = await _healthTrend
            .GenerateHealthReportAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        var (total, signed, unsigned) = await CountReceiptSignaturesAsync(
                tenantId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);

        // Prefer RKSV missing-signature count when available (same period / ambient tenant).
        if (rksv.Summary.TseSignatureMissingCount > unsigned)
            unsigned = rksv.Summary.TseSignatureMissingCount;

        var chainSummary = new TseComplianceSignatureChainSummaryDto
        {
            RegistersChecked = chain.Registers.Count,
            ReceiptsChecked = chain.TotalReceiptsChecked,
            SignatureCount = chain.TotalSignatureCount,
            ChainBreakCount = chain.BreakCount,
            SequenceGapCount = chain.Registers.Sum(r => r.SequenceGapCount),
            DuplicateCount = chain.TotalDuplicateCount,
            MissingSignatureCount = chain.Registers.Sum(r => r.MissingSignatureCount),
            ChainHealthy = chain.BreakCount == 0
                           && chain.TotalGapsCount == 0
                           && chain.TotalDuplicateCount == 0
                           && chain.Registers.Sum(r => r.MissingSignatureCount) == 0,
        };

        var healthSummary = new TseComplianceHealthSummaryDto
        {
            TotalDevices = health.TotalDevices,
            HealthyDevices = health.HealthyDevices,
            DegradedDevices = health.DegradedDevices,
            UnhealthyDevices = health.UnhealthyDevices,
            AverageHealthScore = health.AverageHealthScore,
            HealthyMinScore = health.HealthyMinScore,
            DegradedMinScore = health.DegradedMinScore,
        };

        var issues = BuildIssues(unsigned, rksv, chainSummary, health);
        var recommendations = BuildRecommendations(issues, health);

        var isFullyCompliant = !issues.Any(i =>
            string.Equals(i.Severity, "Critical", StringComparison.OrdinalIgnoreCase)
            || string.Equals(i.Severity, "Warning", StringComparison.OrdinalIgnoreCase));

        _logger.LogInformation(
            "TSE compliance report TenantId={TenantId} Period={From:o}..{To:o} Total={Total} Unsigned={Unsigned} Compliant={Compliant}",
            tenantId,
            fromUtc,
            toUtc,
            total,
            unsigned,
            isFullyCompliant);

        return new TseComplianceReportDto
        {
            TenantId = tenantId,
            TenantName = tenant.Name,
            TenantSlug = tenant.Slug,
            ReportPeriodStart = fromUtc,
            ReportPeriodEnd = toUtc,
            GeneratedAt = generatedAt,
            TotalTransactions = total,
            SignedTransactions = signed,
            UnsignedTransactions = unsigned,
            IsFullyCompliant = isFullyCompliant,
            Issues = issues,
            Recommendations = recommendations,
            HealthSummary = healthSummary,
            SignatureChainSummary = chainSummary,
            LegalNoticeDe = rksv.LegalNoticeDe,
        };
    }

    public async Task<TseComplianceStatusDto> GetComplianceStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddDays(-DefaultStatusLookbackDays);
        var report = await GenerateComplianceReportAsync(tenantId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);

        var hasCritical = report.Issues.Any(i =>
            string.Equals(i.Severity, "Critical", StringComparison.OrdinalIgnoreCase));
        var hasWarning = report.Issues.Any(i =>
            string.Equals(i.Severity, "Warning", StringComparison.OrdinalIgnoreCase));

        var status = hasCritical
            ? TseComplianceStatusNames.NonCompliant
            : hasWarning
                ? TseComplianceStatusNames.AtRisk
                : TseComplianceStatusNames.Compliant;

        return new TseComplianceStatusDto
        {
            TenantId = tenantId,
            TenantName = report.TenantName,
            Status = status,
            IsFullyCompliant = report.IsFullyCompliant,
            TotalTransactions = report.TotalTransactions,
            UnsignedTransactions = report.UnsignedTransactions,
            ChainBreakCount = report.SignatureChainSummary.ChainBreakCount,
            UnhealthyDevices = report.HealthSummary.UnhealthyDevices,
            CheckedAt = report.GeneratedAt,
            LookbackStart = fromUtc,
            LookbackEnd = toUtc,
            TopIssueCodes = report.Issues.Select(i => i.Code).Distinct().Take(8).ToList(),
        };
    }

    private async Task<(int Total, int Signed, int Unsigned)> CountReceiptSignaturesAsync(
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
        return (total, signed, total - signed);
    }

    private static List<TseComplianceIssueDto> BuildIssues(
        int unsigned,
        RksvComplianceReportDto rksv,
        TseComplianceSignatureChainSummaryDto chain,
        TseHealthReportDto health)
    {
        var issues = new List<TseComplianceIssueDto>();

        if (unsigned > 0 || rksv.Summary.TseSignatureMissingCount > 0)
        {
            var count = Math.Max(unsigned, rksv.Summary.TseSignatureMissingCount);
            issues.Add(new TseComplianceIssueDto
            {
                Code = "missing_tse_signature",
                Severity = "Critical",
                Message = $"{count} fiscal receipt(s) missing TSE signature in the report period.",
                Count = count,
            });
        }

        if (chain.ChainBreakCount > 0 || rksv.Summary.SignatureChainBreaks > 0)
        {
            var count = Math.Max(chain.ChainBreakCount, rksv.Summary.SignatureChainBreaks);
            issues.Add(new TseComplianceIssueDto
            {
                Code = "signature_chain_break",
                Severity = "Critical",
                Message = $"{count} signature chain break(s) detected.",
                Count = count,
            });
        }

        if (chain.SequenceGapCount > 0 || rksv.Summary.SequenceGapCount > 0)
        {
            var count = Math.Max(chain.SequenceGapCount, rksv.Summary.SequenceGapCount);
            issues.Add(new TseComplianceIssueDto
            {
                Code = "receipt_sequence_gap",
                Severity = "Warning",
                Message = $"{count} BelegNr sequence gap(s) detected.",
                Count = count,
            });
        }

        if (chain.DuplicateCount > 0)
        {
            issues.Add(new TseComplianceIssueDto
            {
                Code = "receipt_sequence_duplicate",
                Severity = "Warning",
                Message = $"{chain.DuplicateCount} duplicate receipt sequence(s) detected.",
                Count = chain.DuplicateCount,
            });
        }

        if (rksv.Summary.QrFormatInvalidCount > 0)
        {
            issues.Add(new TseComplianceIssueDto
            {
                Code = "qr_payload_invalid",
                Severity = "Warning",
                Message = $"{rksv.Summary.QrFormatInvalidCount} receipt(s) with invalid RKSV QR payload format.",
                Count = rksv.Summary.QrFormatInvalidCount,
            });
        }

        if (health.UnhealthyDevices > 0)
        {
            issues.Add(new TseComplianceIssueDto
            {
                Code = "tse_device_unhealthy",
                Severity = "Critical",
                Message = $"{health.UnhealthyDevices} TSE device(s) are unhealthy/offline.",
                Count = health.UnhealthyDevices,
            });
        }
        else if (health.DegradedDevices > 0)
        {
            issues.Add(new TseComplianceIssueDto
            {
                Code = "tse_device_degraded",
                Severity = "Warning",
                Message = $"{health.DegradedDevices} TSE device(s) are degraded.",
                Count = health.DegradedDevices,
            });
        }

        foreach (var rec in health.Recommendations.Where(r =>
                     string.Equals(r.Severity, "Critical", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(r.Severity, "Warning", StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(new TseComplianceIssueDto
            {
                Code = string.IsNullOrWhiteSpace(rec.Code) ? "health_recommendation" : rec.Code,
                Severity = rec.Severity,
                Message = rec.Message,
                DeviceId = rec.DeviceId,
            });
        }

        return issues
            .GroupBy(i => i.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static List<TseComplianceRecommendationDto> BuildRecommendations(
        IReadOnlyList<TseComplianceIssueDto> issues,
        TseHealthReportDto health)
    {
        var recs = new List<TseComplianceRecommendationDto>();

        if (issues.Any(i => i.Code is "missing_tse_signature"))
        {
            recs.Add(new TseComplianceRecommendationDto
            {
                Code = "replay_offline_queue",
                Severity = "Critical",
                Message =
                    "Review TSE offline NonFiscalPending queue and replay unsigned cash/card intents; verify TSE connectivity.",
            });
        }

        if (issues.Any(i => i.Code is "signature_chain_break"))
        {
            recs.Add(new TseComplianceRecommendationDto
            {
                Code = "inspect_signature_chain",
                Severity = "Critical",
                Message =
                    "Inspect signature-chain continuity per register and produce a DEP §7 export for the affected period.",
            });
        }

        if (issues.Any(i => i.Code is "receipt_sequence_gap" or "receipt_sequence_duplicate"))
        {
            recs.Add(new TseComplianceRecommendationDto
            {
                Code = "review_belegnr_sequence",
                Severity = "Warning",
                Message = "Review BelegNr assignment and integrity checks for gaps/duplicates.",
            });
        }

        if (issues.Any(i => i.Code.StartsWith("tse_device_", StringComparison.Ordinal)))
        {
            recs.Add(new TseComplianceRecommendationDto
            {
                Code = "check_tse_failover",
                Severity = "Warning",
                Message = "Check TSE device health / failover dashboard and certificate validity.",
            });
        }

        foreach (var h in health.Recommendations)
        {
            if (recs.Any(r => string.Equals(r.Code, h.Code, StringComparison.OrdinalIgnoreCase)))
                continue;
            recs.Add(new TseComplianceRecommendationDto
            {
                Code = h.Code,
                Severity = h.Severity,
                Message = h.Message,
                DeviceId = h.DeviceId,
            });
        }

        if (recs.Count == 0)
        {
            recs.Add(new TseComplianceRecommendationDto
            {
                Code = "maintain_monitoring",
                Severity = "Info",
                Message = "No blocking compliance issues in this period. Continue regular DEP exports and health monitoring.",
            });
        }

        return recs;
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
