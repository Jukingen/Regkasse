namespace KasseAPI_Final.Models.Reports;

/// <summary>
/// Timeline payload for a formal report chain (daily, monthly, yearly).
/// Report and submission states are intentionally separated.
/// </summary>
public sealed class ReportHistoryTimelineDto
{
    public string ReportType { get; set; } = string.Empty;
    public Guid RequestedReportId { get; set; }
    public Guid ChainRootReportId { get; set; }
    public Guid? CurrentActiveReportId { get; set; }
    public IReadOnlyList<ReportHistoryItemDto> Items { get; set; } = Array.Empty<ReportHistoryItemDto>();
}

public sealed class ReportHistoryItemDto
{
    public Guid ReportId { get; set; }
    public int ReportVersion { get; set; }
    public string ReportStatus { get; set; } = string.Empty;
    public Guid? OriginalReportId { get; set; }
    public Guid? CorrectionOfReportId { get; set; }
    public Guid? SupersedesReportId { get; set; }
    public Guid? SupersededByReportId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? FinalizedAtUtc { get; set; }
    public bool IsCurrentActiveVersion { get; set; }
    public bool IsOriginalVersion { get; set; }
    public bool IsCorrectionVersion { get; set; }
    public ReportHistorySubmissionSummaryDto Submission { get; set; } = new();
    public IReadOnlyList<string> LabelKeys { get; set; } = Array.Empty<string>();
}

public sealed class ReportHistorySubmissionSummaryDto
{
    public string Lifecycle { get; set; } = "not_submitted";
    public Guid? OutboxMessageId { get; set; }
    public string? OutboxStatus { get; set; }
    public string? LatestStatusCode { get; set; }
    public string? ExternalReferenceId { get; set; }
    public string? LastErrorMessage { get; set; }
    public bool IsSubmitted { get; set; }
    public bool IsAccepted { get; set; }
    public bool IsRejected { get; set; }
    public bool IsRetrying { get; set; }
    public bool HasMissingOutboxReference { get; set; }
}
