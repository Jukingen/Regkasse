using KasseAPI_Final.Models;

namespace KasseAPI_Final.Models.Reports;

/// <summary>Überlagerung: untergeordnete Korrektur betrifft Aggregation — getrennt von FinanzOnline-Submission.</summary>
public sealed class FormalReportUpstreamPropagationDto
{
    public bool RequiresReview { get; set; }
    public string? ReasonCode { get; set; }
    public string? NoteDe { get; set; }
}

public sealed class MonatsberichtDto
{
    public Guid Id { get; set; }
    public DateTime ViennaMonthStart { get; set; }
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
    public MonatsberichtSummaryDto Summary { get; set; } = new();
    public TagesberichtSubmissionStateDto Submission { get; set; } = new();
    public ReportSubmissionEnvelopeDto SubmissionEnvelope { get; set; } = new();
    public TagesberichtCorrectionInfoDto Correction { get; set; } = new();
    public IReadOnlyList<TagesberichtExportProfileDto> ExportProfiles { get; set; } = Array.Empty<TagesberichtExportProfileDto>();
    public FormalReportUpstreamPropagationDto UpstreamPropagation { get; set; } = new();
}

/// <summary>Aylık özet: bağlı günlük raporlar + toplamlar + ham ödeme doğrulaması.</summary>
public sealed class MonatsberichtSummaryDto
{
    public string SchemaVersion { get; set; } = "1.0";
    public string ViennaYearMonth { get; set; } = string.Empty;
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtcExclusive { get; set; }
    public string ScopeKind { get; set; } = string.Empty;
    public Guid? CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }
    public string? StoreLabel { get; set; }

    public IReadOnlyList<LinkedTagesberichtLineDto> LinkedFinalizedTagesberichte { get; set; } =
        Array.Empty<LinkedTagesberichtLineDto>();

    public MonatsberichtAggregationFromDailyDto AggregationFromDaily { get; set; } = new();
    public MonatsberichtRawPaymentRollupDto RawPaymentRollup { get; set; } = new();
    public MonatsberichtAdjustmentDto Adjustment { get; set; } = new();

    public IReadOnlyList<TagesberichtPaymentMethodBreakdownDto> PaymentMethodBreakdown { get; set; } =
        Array.Empty<TagesberichtPaymentMethodBreakdownDto>();
    public IReadOnlyList<TagesberichtTaxBreakdownDto> TaxBreakdown { get; set; } = Array.Empty<TagesberichtTaxBreakdownDto>();

    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public sealed class LinkedTagesberichtLineDto
{
    public Guid TagesberichtId { get; set; }
    public DateTime ViennaBusinessDate { get; set; }
    public Guid CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }
    public string SnapshotHash { get; set; } = string.Empty;
    public decimal GrossSalesAmount { get; set; }
}

public sealed class MonatsberichtAggregationFromDailyDto
{
    public int LinkedDailyReportCount { get; set; }
    public int ExpectedCalendarDaysInMonth { get; set; }
    public int DistinctDaysCovered { get; set; }
    public decimal GrossSalesAmount { get; set; }
    public decimal TaxTotalAmount { get; set; }
    public decimal RefundAmountTotal { get; set; }
    public int SalePaymentRowCount { get; set; }
    public int RefundRowCount { get; set; }
    public int StornoRowCount { get; set; }
}

public sealed class MonatsberichtRawPaymentRollupDto
{
    public decimal GrossSalesAmount { get; set; }
    public decimal TaxTotalAmount { get; set; }
    public decimal RefundAmountTotal { get; set; }
    public int SalePaymentRowCount { get; set; }
}

public sealed class MonatsberichtAdjustmentDto
{
    public decimal GrossDeltaDailyVsRaw { get; set; }
    public bool RequiresReview { get; set; }
    public string? NoteDe { get; set; }
}

public sealed class MonatsberichtListItemDto
{
    public Guid Id { get; set; }
    public DateTime ViennaMonthStart { get; set; }
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
    public bool UpstreamReviewRequired { get; set; }
    public string? UpstreamReviewReasonCode { get; set; }
}

public sealed class MonatsberichtGenerationRequest
{
    /// <summary>Yıl-ay (Vienna), ayın 1. günü veya herhangi bir gün aynı ay içinde.</summary>
    public DateTime ViennaMonthAnyDay { get; set; }

    public string ScopeKind { get; set; } = MonatsberichtScopeKinds.Register;

    /// <summary>Register kapsamı için zorunlu; Company için null.</summary>
    public Guid? CashRegisterId { get; set; }

    public bool ForceNewProvisional { get; set; }
}

public sealed class MonatsberichtFinalizeRequest
{
    public Guid ReportId { get; set; }
    public string? Note { get; set; }
}

public sealed class MonatsberichtCorrectionRequest
{
    public Guid SupersedesReportId { get; set; }
    public string? Reason { get; set; }
}
