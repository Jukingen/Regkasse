using KasseAPI_Final.Models;

namespace KasseAPI_Final.Models.Reports;

public sealed class JahresberichtDto
{
    public Guid Id { get; set; }
    public DateTime ViennaYearStart { get; set; }
    public string ScopeKind { get; set; } = string.Empty;
    public Guid? CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }
    public string? StoreLabel { get; set; }
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
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime? FinalizedAtUtc { get; set; }
    public string? FinalizedByUserId { get; set; }
    public string SnapshotSchemaVersion { get; set; } = string.Empty;
    public string SnapshotHash { get; set; } = string.Empty;
    public JahresberichtSummaryDto Summary { get; set; } = new();
    public TagesberichtSubmissionStateDto Submission { get; set; } = new();
    public ReportSubmissionEnvelopeDto SubmissionEnvelope { get; set; } = new();
    public TagesberichtCorrectionInfoDto Correction { get; set; } = new();
    public IReadOnlyList<TagesberichtExportProfileDto> ExportProfiles { get; set; } = Array.Empty<TagesberichtExportProfileDto>();
}

public sealed class JahresberichtSummaryDto
{
    public string SchemaVersion { get; set; } = "1.0";
    public int ViennaYear { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtcExclusive { get; set; }
    public string ScopeKind { get; set; } = string.Empty;
    public Guid? CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }
    public string? StoreLabel { get; set; }

    public IReadOnlyList<LinkedMonatsberichtLineDto> LinkedFinalizedMonatsberichte { get; set; } =
        Array.Empty<LinkedMonatsberichtLineDto>();

    public JahresberichtAggregationFromMonthlyDto AggregationFromMonthly { get; set; } = new();
    public JahresberichtRawPaymentRollupDto RawPaymentRollup { get; set; } = new();
    public JahresberichtAdjustmentDto Adjustment { get; set; } = new();

    public IReadOnlyList<TagesberichtPaymentMethodBreakdownDto> PaymentMethodBreakdown { get; set; } =
        Array.Empty<TagesberichtPaymentMethodBreakdownDto>();
    public IReadOnlyList<TagesberichtTaxBreakdownDto> TaxBreakdown { get; set; } = Array.Empty<TagesberichtTaxBreakdownDto>();

    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public sealed class LinkedMonatsberichtLineDto
{
    public Guid MonatsberichtId { get; set; }
    public DateTime ViennaMonthStart { get; set; }
    public Guid? CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }
    public string SnapshotHash { get; set; } = string.Empty;
    public decimal GrossSalesAmount { get; set; }
    public string ReportStatus { get; set; } = string.Empty;
}

public sealed class JahresberichtAggregationFromMonthlyDto
{
    public int LinkedMonthlyReportCount { get; set; }
    public int ExpectedMonthsInYear { get; set; }
    public int DistinctMonthsCovered { get; set; }
    public decimal GrossSalesAmount { get; set; }
    public decimal TaxTotalAmount { get; set; }
    public decimal RefundAmountTotal { get; set; }
    public int SalePaymentRowCount { get; set; }
    public int RefundRowCount { get; set; }
    public int StornoRowCount { get; set; }
}

public sealed class JahresberichtRawPaymentRollupDto
{
    public decimal GrossSalesAmount { get; set; }
    public decimal TaxTotalAmount { get; set; }
    public decimal RefundAmountTotal { get; set; }
    public int SalePaymentRowCount { get; set; }
}

public sealed class JahresberichtAdjustmentDto
{
    public decimal GrossDeltaMonthlyVsRaw { get; set; }
    public bool RequiresReview { get; set; }
    public string? NoteDe { get; set; }
}

public sealed class JahresberichtListItemDto
{
    public Guid Id { get; set; }
    public DateTime ViennaYearStart { get; set; }
    public string ScopeKind { get; set; } = string.Empty;
    public Guid? CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }
    public string ReportStatus { get; set; } = string.Empty;
    public string CorrectionKind { get; set; } = string.Empty;
    public int ReportVersion { get; set; }
    public string SubmissionImpact { get; set; } = string.Empty;
    public decimal GrossSalesAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public TagesberichtSubmissionStateDto Submission { get; set; } = new();
}

public sealed class JahresberichtGenerationRequest
{
    public DateTime ViennaYearAnyDay { get; set; }
    public string ScopeKind { get; set; } = MonatsberichtScopeKinds.Register;
    public Guid? CashRegisterId { get; set; }
    public bool ForceNewProvisional { get; set; }
}

public sealed class JahresberichtFinalizeRequest
{
    public Guid ReportId { get; set; }
    public string? Note { get; set; }
}

public sealed class JahresberichtCorrectionRequest
{
    public Guid SupersedesReportId { get; set; }
    public string? Reason { get; set; }
}
