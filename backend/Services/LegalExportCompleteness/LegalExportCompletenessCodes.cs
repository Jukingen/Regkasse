namespace KasseAPI_Final.Services.LegalExportCompleteness;

/// <summary>Legal export completeness issue codes (stable API contract).</summary>
public static class LegalExportCompletenessCodes
{
    public const string ProvisionalNotFinalized = "provisional_not_finalized";
    public const string StaleSupersededChain = "stale_superseded_chain";
    public const string IncompletePaymentMapping = "incomplete_payment_mapping";
    public const string UnresolvedOfflineReplayGap = "unresolved_offline_replay_gap";
    public const string MissingTaxClassification = "missing_tax_classification";
    public const string MissingClosingReferences = "missing_closing_references";
    public const string IncompleteAggregationCoverage = "incomplete_aggregation_coverage";
    public const string MissingPeriodMetadata = "missing_period_metadata";
    public const string UpstreamReviewRequired = "upstream_review_required";
    public const string AdjustmentRequiresReview = "adjustment_requires_review";
    public const string LinkedReportNotFinalized = "linked_report_not_finalized";
    public const string SubmissionNotProofOfCompleteness = "submission_not_proof_of_completeness";
}
