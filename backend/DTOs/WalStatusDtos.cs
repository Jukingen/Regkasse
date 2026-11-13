using System.Text.Json.Serialization;

namespace KasseAPI_Final.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WalArchiveHealthLevel
{
    Healthy,
    Degraded,
    Critical,
}

/// <summary>
/// Read-only WAL archival health. Segment names are file names only — never paths, bytes, or secrets.
/// </summary>
public sealed class WalStatusDto
{
    public DateTime? LastSuccessfulSegmentAtUtc { get; init; }

    public string? LastSuccessfulSegmentName { get; init; }

    public DateTime? LastAttemptedSegmentAtUtc { get; init; }

    public int? LagSeconds { get; init; }

    public int ArchivedCountLast24h { get; init; }

    public int FailedCountLast24h { get; init; }

    public string? LastFailureReason { get; init; }

    public WalArchiveHealthLevel HealthLevel { get; init; }

    public string HealthReason { get; init; } = string.Empty;
}

/// <summary>
/// In-process snapshot produced by <c>WalArchiveService</c>. Not an API response.
/// </summary>
public sealed class WalArchiveObservation
{
    /// <summary><c>Backup:PitrWalArchivingDeclaredEnabled</c>.</summary>
    public bool Configured { get; init; }

    public DateTime? LastSuccessfulSegmentAtUtc { get; init; }

    public string? LastSuccessfulSegmentName { get; init; }

    public DateTime? LastAttemptedSegmentAtUtc { get; init; }

    public int ArchivedCountLast24h { get; init; }

    public int FailedCountLast24h { get; init; }

    public string? LastFailureReason { get; init; }
}
