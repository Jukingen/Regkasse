namespace KasseAPI_Final.Models.Reports;

/// <summary>
/// LegalComplianceExport öncesi bütünlük kapısı — OperationalPreview/DiagnosticPackage ile karıştırılmamalı.
/// </summary>
public sealed class LegalExportCompletenessIssueDto
{
    /// <summary>Makine okunur kod (örn. incomplete_payment_mapping).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>block: Legal export yapılmamalı; warn: izinli ancak uyarılı.</summary>
    public string Severity { get; set; } = "warn";

    public string MessageDe { get; set; } = string.Empty;

    public string MessageEn { get; set; } = string.Empty;
}

public sealed class LegalExportCompletenessResultDto
{
    /// <summary>allowed | allowed_with_warnings | blocked</summary>
    public string Gate { get; set; } = "allowed";

    public string ReportType { get; set; } = string.Empty;

    public Guid ReportId { get; set; }

    public IReadOnlyList<LegalExportCompletenessIssueDto> Issues { get; set; } = Array.Empty<LegalExportCompletenessIssueDto>();
}
