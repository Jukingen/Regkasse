using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services.LegalExportCompleteness;
using Xunit;

namespace KasseAPI_Final.Tests;

public class LegalExportCompletenessEvaluatorTests
{
    [Fact]
    public void Tagesbericht_Finalized_Clean_Allowed()
    {
        var dto = BaseTagesbericht();
        dto.ReportStatus = "Finalized";
        dto.SupersededByReportId = null;
        dto.Summary.Reconciliation = new TagesberichtReconciliationFlagsDto
        {
            UnknownPaymentMethodRowCount = 0,
            OfflineLinkedPaymentCount = 0,
            DayClosedInRksv = true,
            DailyClosingId = Guid.NewGuid()
        };
        dto.LinkedDailyClosingId = Guid.NewGuid();
        dto.Summary.TaxBreakdown = new List<TagesberichtTaxBreakdownDto>
        {
            new() { TaxBucketKey = "standard", TaxAmount = 1, NetHint = 0 }
        };

        var r = LegalExportCompletenessEvaluator.EvaluateTagesbericht(dto);
        Assert.Equal(LegalExportCompletenessEvaluator.GateAllowed, r.Gate);
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void Tagesbericht_Provisional_Blocked()
    {
        var dto = BaseTagesbericht();
        dto.ReportStatus = "Provisional";
        var r = LegalExportCompletenessEvaluator.EvaluateTagesbericht(dto);
        Assert.Equal(LegalExportCompletenessEvaluator.GateBlocked, r.Gate);
        Assert.Contains(r.Issues, i => i.Code == LegalExportCompletenessCodes.ProvisionalNotFinalized);
    }

    [Fact]
    public void Tagesbericht_Superseded_Blocked()
    {
        var dto = BaseTagesbericht();
        dto.ReportStatus = "Finalized";
        dto.SupersededByReportId = Guid.NewGuid();
        var r = LegalExportCompletenessEvaluator.EvaluateTagesbericht(dto);
        Assert.Equal(LegalExportCompletenessEvaluator.GateBlocked, r.Gate);
        Assert.Contains(r.Issues, i => i.Code == LegalExportCompletenessCodes.StaleSupersededChain);
    }

    [Fact]
    public void Tagesbericht_UnknownPayment_Blocked()
    {
        var dto = BaseTagesbericht();
        dto.ReportStatus = "Finalized";
        dto.Summary.Reconciliation.UnknownPaymentMethodRowCount = 2;
        var r = LegalExportCompletenessEvaluator.EvaluateTagesbericht(dto);
        Assert.Equal(LegalExportCompletenessEvaluator.GateBlocked, r.Gate);
        Assert.Contains(r.Issues, i => i.Code == LegalExportCompletenessCodes.IncompletePaymentMapping);
    }

    [Fact]
    public void Monatsbericht_UpstreamReview_Blocked()
    {
        var dto = BaseMonatsbericht();
        dto.ReportStatus = "Finalized";
        dto.UpstreamPropagation = new FormalReportUpstreamPropagationDto { RequiresReview = true, NoteDe = "Test" };
        var r = LegalExportCompletenessEvaluator.EvaluateMonatsbericht(dto);
        Assert.Equal(LegalExportCompletenessEvaluator.GateBlocked, r.Gate);
        Assert.Contains(r.Issues, i => i.Code == LegalExportCompletenessCodes.UpstreamReviewRequired);
    }

    [Fact]
    public void Jahresbericht_LinkedMonthNotFinalized_Blocked()
    {
        var dto = BaseJahresbericht();
        dto.ReportStatus = "Finalized";
        dto.Summary.LinkedFinalizedMonatsberichte = new List<LinkedMonatsberichtLineDto>
        {
            new()
            {
                MonatsberichtId = Guid.NewGuid(),
                ViennaMonthStart = new DateTime(2025, 3, 1),
                ReportStatus = "Provisional",
                SnapshotHash = "x",
                GrossSalesAmount = 1
            }
        };
        var r = LegalExportCompletenessEvaluator.EvaluateJahresbericht(dto);
        Assert.Equal(LegalExportCompletenessEvaluator.GateBlocked, r.Gate);
        Assert.Contains(r.Issues, i => i.Code == LegalExportCompletenessCodes.LinkedReportNotFinalized);
    }

    [Fact]
    public void Periodenbericht_MissingHashes_Blocked()
    {
        var dto = new PeriodenberichtRunDto
        {
            Id = Guid.NewGuid(),
            SnapshotSchemaVersion = "",
            SnapshotHash = "",
            QueryParametersHash = "",
            Summary = new OperationalSummaryDto(),
            Warnings = Array.Empty<string>()
        };
        var r = LegalExportCompletenessEvaluator.EvaluatePeriodenbericht(dto);
        Assert.Equal(LegalExportCompletenessEvaluator.GateBlocked, r.Gate);
        Assert.Contains(r.Issues, i => i.Code == LegalExportCompletenessCodes.MissingPeriodMetadata);
    }

    private static TagesberichtDto BaseTagesbericht() =>
        new()
        {
            Id = Guid.NewGuid(),
            ReportStatus = "Finalized",
            Summary = new TagesberichtSummaryDto
            {
                SalePaymentRowCount = 1,
                GrossSalesAmount = 10,
                Reconciliation = new TagesberichtReconciliationFlagsDto(),
                TaxBreakdown = Array.Empty<TagesberichtTaxBreakdownDto>(),
                PaymentMethodBreakdown = Array.Empty<TagesberichtPaymentMethodBreakdownDto>()
            },
            Submission = new TagesberichtSubmissionStateDto()
        };

    private static MonatsberichtDto BaseMonatsbericht() =>
        new()
        {
            Id = Guid.NewGuid(),
            ReportStatus = "Finalized",
            Summary = new MonatsberichtSummaryDto
            {
                AggregationFromDaily = new MonatsberichtAggregationFromDailyDto(),
                Adjustment = new MonatsberichtAdjustmentDto(),
                PaymentMethodBreakdown = Array.Empty<TagesberichtPaymentMethodBreakdownDto>(),
                TaxBreakdown = Array.Empty<TagesberichtTaxBreakdownDto>()
            },
            Submission = new TagesberichtSubmissionStateDto(),
            UpstreamPropagation = new FormalReportUpstreamPropagationDto()
        };

    private static JahresberichtDto BaseJahresbericht() =>
        new()
        {
            Id = Guid.NewGuid(),
            ReportStatus = "Finalized",
            Summary = new JahresberichtSummaryDto
            {
                AggregationFromMonthly = new JahresberichtAggregationFromMonthlyDto(),
                Adjustment = new JahresberichtAdjustmentDto(),
                LinkedFinalizedMonatsberichte = Array.Empty<LinkedMonatsberichtLineDto>(),
                PaymentMethodBreakdown = Array.Empty<TagesberichtPaymentMethodBreakdownDto>(),
                TaxBreakdown = Array.Empty<TagesberichtTaxBreakdownDto>()
            },
            Submission = new TagesberichtSubmissionStateDto(),
            UpstreamPropagation = new FormalReportUpstreamPropagationDto()
        };
}
