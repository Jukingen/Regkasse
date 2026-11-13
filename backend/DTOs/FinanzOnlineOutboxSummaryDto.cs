namespace KasseAPI_Final.DTOs;

/// <summary>Dashboard snapshot of the FinanzOnline submission outbox. Times are UTC.</summary>
public sealed class FinanzOnlineOutboxSummaryDto
{
    public int PendingCount { get; init; }

    /// <summary>Failed rows whose last processing time (or creation time) falls in the last 24 hours.</summary>
    public int FailedLast24Hours { get; init; }

    public DateTime? LastSuccessfulSubmissionUtc { get; init; }
}
