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
    public int TotalTransactions { get; set; }
    public int UnsignedTransactions { get; set; }
    public int ChainBreakCount { get; set; }
    public int UnhealthyDevices { get; set; }
    public DateTime CheckedAt { get; set; }
    public DateTime LookbackStart { get; set; }
    public DateTime LookbackEnd { get; set; }
    public IReadOnlyList<string> TopIssueCodes { get; set; } = Array.Empty<string>();
}

public static class TseComplianceStatusNames
{
    public const string Compliant = "Compliant";
    public const string AtRisk = "AtRisk";
    public const string NonCompliant = "NonCompliant";
}
