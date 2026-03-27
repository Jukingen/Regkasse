namespace KasseAPI_Final.Models.Reports;

public sealed class ReportSubmissionEnvelopeDto
{
    public string ReportType { get; set; } = string.Empty;
    public Guid ReportId { get; set; }
    public string ReportState { get; set; } = string.Empty;
    public string SubmissionState { get; set; } = "not_submitted";
    public Guid? OutboxMessageId { get; set; }
    public string? OutboxStatus { get; set; }
    public string? CorrelationId { get; set; }
    public string? BusinessKey { get; set; }
    public string? MessageType { get; set; }
    public string? AggregateType { get; set; }
    public string? LegalExportPackageReference { get; set; }
    public string? SupersedesReportReference { get; set; }
    public string? SupersededByReportReference { get; set; }
    public ReportSubmissionStateDto State { get; set; } = new();
    public RetryPolicyDto RetryPolicy { get; set; } = new();
    public IReadOnlyList<SubmissionAttemptDto> Attempts { get; set; } = Array.Empty<SubmissionAttemptDto>();
    public IReadOnlyList<string> RejectionReasons { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> RemediationHintsDe { get; set; } = Array.Empty<string>();
}

public sealed class ReportSubmissionStateDto
{
    public string Lifecycle { get; set; } = "not_submitted";
    public bool IsTerminal { get; set; }
    public bool IsAccepted { get; set; }
    public bool RequiresCorrection { get; set; }
    public bool RetryScheduled { get; set; }
    public string? ExternalReferenceId { get; set; }
    public string? TransmissionId { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
}

public sealed class SubmissionAttemptDto
{
    public int AttemptCount { get; set; }
    public string? Status { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string? FailureCategory { get; set; }
    public SubmissionResultDto Result { get; set; } = new();
}

public sealed class SubmissionResultDto
{
    public bool Success { get; set; }
    public bool AwaitingAcknowledgement { get; set; }
    public bool AcknowledgementMissing { get; set; }
    public string? ProtocolCode { get; set; }
    public string? ExternalStatus { get; set; }
}

public sealed class RetryPolicyDto
{
    public int MaxAttempts { get; set; }
    public int BaseDelaySeconds { get; set; }
    public int BackoffCapSeconds { get; set; }
    public int JitterMaxSeconds { get; set; }
    public bool IdempotentEnqueue { get; set; }
}

public sealed class BuildReportSubmissionEnvelopeRequest
{
    public string ReportType { get; set; } = string.Empty;
    public Guid ReportId { get; set; }
    public string ReportState { get; set; } = string.Empty;
    public Guid? OutboxMessageId { get; set; }
    public Guid? SupersedesReportId { get; set; }
    public Guid? SupersededByReportId { get; set; }
    public string? LegalExportPackageReference { get; set; }
}
