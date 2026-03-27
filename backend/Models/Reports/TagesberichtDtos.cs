namespace KasseAPI_Final.Models.Reports;

/// <summary>Liste ve detay için ana DTO.</summary>
public sealed class TagesberichtDto
{
    public Guid Id { get; set; }
    public DateTime ViennaBusinessDate { get; set; }
    public Guid CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }
    public string? StoreLabel { get; set; }
    public string? OperatorUserIdScope { get; set; }
    public string ReportStatus { get; set; } = string.Empty;
    public string CorrectionKind { get; set; } = string.Empty;
    public Guid? OriginalReportId { get; set; }
    public Guid? CorrectionOfReportId { get; set; }
    public Guid? SupersedesReportId { get; set; }
    public Guid? SupersededByReportId { get; set; }
    public int ReportVersion { get; set; }
    public string? ReportRevisionReason { get; set; }
    public string? RebuildCause { get; set; }
    public string CorrectionType { get; set; } = string.Empty;
    public string SubmissionImpact { get; set; } = string.Empty;
    public Guid? LinkedDailyClosingId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime? FinalizedAtUtc { get; set; }
    public string? FinalizedByUserId { get; set; }
    public string SnapshotSchemaVersion { get; set; } = string.Empty;
    public string SnapshotHash { get; set; } = string.Empty;
    public TagesberichtSummaryDto Summary { get; set; } = new();
    public TagesberichtSubmissionStateDto Submission { get; set; } = new();
    public ReportSubmissionEnvelopeDto SubmissionEnvelope { get; set; } = new();
    public TagesberichtCorrectionInfoDto Correction { get; set; } = new();
    public IReadOnlyList<TagesberichtExportProfileDto> ExportProfiles { get; set; } = Array.Empty<TagesberichtExportProfileDto>();
}

public sealed class TagesberichtSummaryDto
{
    public string SchemaVersion { get; set; } = "1.0";
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtcExclusive { get; set; }
    public DateTime ViennaBusinessDate { get; set; }
    public Guid CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }
    public string? StoreLabel { get; set; }
    public string? OperatorUserIdScope { get; set; }
    public int SalePaymentRowCount { get; set; }
    public int RefundRowCount { get; set; }
    public int StornoRowCount { get; set; }
    public decimal GrossSalesAmount { get; set; }
    public decimal TaxTotalAmount { get; set; }
    public decimal RefundAmountTotal { get; set; }
    public IReadOnlyList<TagesberichtPaymentMethodBreakdownDto> PaymentMethodBreakdown { get; set; } =
        Array.Empty<TagesberichtPaymentMethodBreakdownDto>();
    public IReadOnlyList<TagesberichtTaxBreakdownDto> TaxBreakdown { get; set; } = Array.Empty<TagesberichtTaxBreakdownDto>();
    public TagesberichtReconciliationFlagsDto Reconciliation { get; set; } = new();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    public IReadOnlyList<Guid> TracePaymentDetailIds { get; set; } = Array.Empty<Guid>();
    public string TracePaymentIdsHash { get; set; } = string.Empty;
}

public sealed class TagesberichtPaymentMethodBreakdownDto
{
    public string MethodKey { get; set; } = string.Empty;
    public string? DisplayLabel { get; set; }
    public int RowCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class TagesberichtTaxBreakdownDto
{
    public string TaxBucketKey { get; set; } = string.Empty;
    public decimal TaxAmount { get; set; }
    public decimal NetHint { get; set; }
}

public sealed class TagesberichtReconciliationFlagsDto
{
    public int PaymentsWithoutInvoiceCount { get; set; }
    public int UnknownPaymentMethodRowCount { get; set; }
    public int OfflineLinkedPaymentCount { get; set; }
    public bool DayClosedInRksv { get; set; }
    public Guid? DailyClosingId { get; set; }
}

public sealed class TagesberichtSubmissionStateDto
{
    public string Lifecycle { get; set; } = "not_submitted";
    public Guid? FinanzOnlineOutboxMessageId { get; set; }
    public string? OutboxStatus { get; set; }
    public string? ExternalReferenceId { get; set; }
    public string? TransmissionId { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? OperatorHintDe { get; set; }
}

public sealed class TagesberichtCorrectionInfoDto
{
    public bool IsCorrection { get; set; }
    public Guid? SupersedesReportId { get; set; }
    public Guid? SupersededByReportId { get; set; }
    public string? Reason { get; set; }
}

/// <summary>UI profili: operationalPreview / accountingReport / legalComplianceExport / diagnosticPackage.</summary>
public sealed class TagesberichtExportProfileDto
{
    public string ProfileKey { get; set; } = string.Empty;
    public string LabelDe { get; set; } = string.Empty;
    public string DescriptionDe { get; set; } = string.Empty;
    public bool NonLegalOutput { get; set; }
    public bool IsLegalProfile { get; set; }
    public bool IsDiagnosticOnly { get; set; }
    public bool IncludeTraceIds { get; set; }
    public bool IncludeTechnicalHashes { get; set; }
    public bool IncludeReconciliationWarnings { get; set; }
}

public sealed class TagesberichtGenerationRequest
{
    public DateTime ViennaBusinessDate { get; set; }
    public Guid CashRegisterId { get; set; }
    public string? OperatorUserIdScope { get; set; }
    public bool ForceNewProvisional { get; set; }
}

public sealed class TagesberichtFinalizeRequest
{
    public Guid ReportId { get; set; }
    public string? Note { get; set; }
}

public sealed class TagesberichtCorrectionRequest
{
    public Guid SupersedesReportId { get; set; }
    public string? Reason { get; set; }
}

public sealed class TagesberichtListItemDto
{
    public Guid Id { get; set; }
    public DateTime ViennaBusinessDate { get; set; }
    public Guid CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }
    public string ReportStatus { get; set; } = string.Empty;
    public string CorrectionKind { get; set; } = string.Empty;
    public int ReportVersion { get; set; }
    public string SubmissionImpact { get; set; } = string.Empty;
    public decimal GrossSalesAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public TagesberichtSubmissionStateDto Submission { get; set; } = new();
}
