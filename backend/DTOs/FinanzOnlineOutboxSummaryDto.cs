namespace KasseAPI_Final.DTOs;

/// <summary>Dashboard snapshot of the FinanzOnline submission outbox. Times are UTC.</summary>
public sealed class FinanzOnlineOutboxSummaryDto
{
    public int PendingCount { get; init; }

    /// <summary>Failed rows whose last processing time (or creation time) falls in the last 24 hours.</summary>
    public int FailedLast24Hours { get; init; }

    public DateTime? LastSuccessfulSubmissionUtc { get; init; }
}

/// <summary>
/// Outbox health for operators. Recent errors carry a code and a redacted message, never payload or identity fields.
/// </summary>
public sealed class FinanzOnlineOutboxHealthDto
{
    public int PendingCount { get; init; }

    public int FailedLast24Hours { get; init; }

    public DateTime? LastSuccessfulSubmissionUtc { get; init; }

    public IReadOnlyList<FinanzOnlineOutboxRecentErrorDto> RecentErrors { get; init; } =
        Array.Empty<FinanzOnlineOutboxRecentErrorDto>();
}

public sealed class FinanzOnlineOutboxRecentErrorDto
{
    public string Status { get; init; } = string.Empty;

    public string? ErrorCode { get; init; }

    /// <summary>Operator text with email addresses and long digit runs removed.</summary>
    public string? Message { get; init; }

    public DateTime OccurredAtUtc { get; init; }
}
