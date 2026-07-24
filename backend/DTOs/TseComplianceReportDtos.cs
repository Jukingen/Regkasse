namespace KasseAPI_Final.DTOs;

/// <summary>
/// Diagnostic TSE/RKSV compliance snapshot for a tenant period.
/// Not a legally binding Finanzamt proof — see <see cref="LegalNoticeDe"/>.
/// </summary>
public sealed class TseComplianceReportDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantSlug { get; set; }
    public DateTime ReportPeriodStart { get; set; }
    public DateTime ReportPeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; }

    public int TotalTransactions { get; set; }
    public int SignedTransactions { get; set; }
    public int UnsignedTransactions { get; set; }

    public bool IsFullyCompliant { get; set; }
    public IReadOnlyList<TseComplianceIssueDto> Issues { get; set; } = Array.Empty<TseComplianceIssueDto>();
    public IReadOnlyList<TseComplianceRecommendationDto> Recommendations { get; set; } =
        Array.Empty<TseComplianceRecommendationDto>();

    public TseComplianceHealthSummaryDto HealthSummary { get; set; } = new();
    public TseComplianceSignatureChainSummaryDto SignatureChainSummary { get; set; } = new();

    public string LegalNoticeDe { get; set; } =
        "Dieser Bericht ist kein rechtsverbindlicher RKSV-/Finanzamt-Beleg. "
        + "Nur für interne Compliance- und Diagnose-Zwecke. Originalbeleg mit TSE-Signatur ist maßgeblich.";
}

public sealed class TseComplianceIssueDto
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Message { get; set; } = string.Empty;
    public Guid? CashRegisterId { get; set; }
    public Guid? DeviceId { get; set; }
    public int? Count { get; set; }
}

public sealed class TseComplianceRecommendationDto
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public Guid? DeviceId { get; set; }
}

public sealed class TseComplianceHealthSummaryDto
{
    public int TotalDevices { get; set; }
    public int HealthyDevices { get; set; }
    public int DegradedDevices { get; set; }
    public int UnhealthyDevices { get; set; }
    public double AverageHealthScore { get; set; }
    public int HealthyMinScore { get; set; }
    public int DegradedMinScore { get; set; }
}

public sealed class TseComplianceSignatureChainSummaryDto
{
    public int RegistersChecked { get; set; }
    public int ReceiptsChecked { get; set; }
    public int SignatureCount { get; set; }
    public int ChainBreakCount { get; set; }
    public int SequenceGapCount { get; set; }
    public int DuplicateCount { get; set; }
    public int MissingSignatureCount { get; set; }
    public bool ChainHealthy { get; set; }
}

/// <summary>Lightweight current compliance posture (rolling lookback).</summary>
public sealed class TseComplianceStatusDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string Status { get; set; } = "Compliant";
    public bool IsFullyCompliant { get; set; }
    /// <summary>0–100 audit-readiness score derived from issues and coverage.</summary>
    public int ComplianceScore { get; set; }
    public int TotalTransactions { get; set; }
    public int UnsignedTransactions { get; set; }
    public int ChainBreakCount { get; set; }
    public int UnhealthyDevices { get; set; }
    public DateTime CheckedAt { get; set; }
    public DateTime LookbackStart { get; set; }
    public DateTime LookbackEnd { get; set; }
    public IReadOnlyList<string> TopIssueCodes { get; set; } = Array.Empty<string>();
}

/// <summary>FA audit-readiness dashboard (diagnostic; not a Finanzamt proof).</summary>
public sealed class TseComplianceDashboardDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantSlug { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; }

    public int ComplianceScore { get; set; }
    public string Status { get; set; } = TseComplianceStatusNames.Compliant;
    /// <summary>Healthy | AtRisk | Broken</summary>
    public string SignatureChainStatus { get; set; } = "Healthy";
    public int ValidCertificates { get; set; }
    public int TotalCertificates { get; set; }
    public int AuditLogCount { get; set; }

    public TseComplianceReportDto Report { get; set; } = new();
    public IReadOnlyList<TseComplianceCertificateRowDto> Certificates { get; set; } =
        Array.Empty<TseComplianceCertificateRowDto>();
    public TseComplianceTransactionSummaryDto Transactions { get; set; } = new();
    public IReadOnlyList<TseComplianceAuditTrailItemDto> AuditTrail { get; set; } =
        Array.Empty<TseComplianceAuditTrailItemDto>();

    public string LegalNoticeDe { get; set; } =
        "Dieser Bericht ist kein rechtsverbindlicher RKSV-/Finanzamt-Beleg. "
        + "Nur für interne Compliance- und Diagnose-Zwecke.";
}

public sealed class TseComplianceCertificateRowDto
{
    public Guid DeviceId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string CertificateStatus { get; set; } = "UNKNOWN";
    public string LifecycleStatus { get; set; } = "Unknown";
    public bool IsValid { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public double? DaysUntilExpiry { get; set; }
    public int HealthScore { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public DateTime? ScheduledRenewalAt { get; set; }
}

public sealed class TseComplianceTransactionSummaryDto
{
    public int TotalTransactions { get; set; }
    public int SignedTransactions { get; set; }
    public int UnsignedTransactions { get; set; }
    public double SignedPercent { get; set; }
    public bool SignatureChainHealthy { get; set; }
    public int ChainBreakCount { get; set; }
    public int SequenceGapCount { get; set; }
    public int DuplicateCount { get; set; }
    public int MissingSignatureCount { get; set; }
    public IReadOnlyList<TseComplianceIssueDto> Issues { get; set; } = Array.Empty<TseComplianceIssueDto>();
}

public sealed class TseComplianceAuditTrailItemDto
{
    public Guid Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
}

public static class TseComplianceStatusNames
{
    public const string Compliant = "Compliant";
    public const string AtRisk = "AtRisk";
    public const string NonCompliant = "NonCompliant";
}

public static class TseComplianceChainStatusNames
{
    public const string Healthy = "Healthy";
    public const string AtRisk = "AtRisk";
    public const string Broken = "Broken";
}
