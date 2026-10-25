using System.Text.RegularExpressions;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Fixed WAL health bands. No config in this turn.
/// Healthy: lag &lt; 60s and no failures in the last 24 hours.
/// Degraded: lag &lt; 300s (or a recent failure that is still under the critical count).
/// Critical: lag ≥ 300s, no successful segment, ≥ 3 failures in 24 hours, or archiving disabled by config.
/// </summary>
public static class WalStatusEvaluator
{
    public const int HealthyLagExclusiveSeconds = 60;
    public const int CriticalLagSeconds = 300;
    public const int CriticalFailureCount = 3;

    public const string DisabledReason = "wal-archive-disabled";
    public const string NoSegmentReason = "No WAL segment has been archived.";
    public const string FailuresReason = "WAL archive failed at least 3 times in the last 24 hours.";
    public const string LagCriticalReason = "WAL lag is at least 5 minutes.";
    public const string LagDegradedReason = "WAL lag is under 5 minutes.";
    public const string RecentFailuresReason = "WAL archive had recent failures.";
    public const string HealthyReason = "WAL archive is current.";

    private static readonly Regex SafeSegmentName = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SafeFailureToken = new(
        "^[a-z0-9]+(?:-[a-z0-9]+){0,8}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static WalStatusDto Evaluate(WalArchiveObservation observation, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var now = AsUtc(utcNow);
        var segmentName = SanitizeSegmentName(observation.LastSuccessfulSegmentName);
        var failureReason = SanitizeFailureReason(observation.LastFailureReason);
        var archived = Math.Max(0, observation.ArchivedCountLast24h);
        var failed = Math.Max(0, observation.FailedCountLast24h);
        var successAt = observation.LastSuccessfulSegmentAtUtc.HasValue
            ? AsUtc(observation.LastSuccessfulSegmentAtUtc.Value)
            : (DateTime?)null;
        var attemptedAt = observation.LastAttemptedSegmentAtUtc.HasValue
            ? AsUtc(observation.LastAttemptedSegmentAtUtc.Value)
            : successAt;

        int? lag = null;
        if (successAt.HasValue)
        {
            var seconds = (now - successAt.Value).TotalSeconds;
            lag = seconds <= 0 ? 0 : seconds >= int.MaxValue ? int.MaxValue : (int)Math.Floor(seconds);
        }

        var (level, reason) = Classify(observation.Configured, successAt, lag, failed);
        return new WalStatusDto
        {
            LastSuccessfulSegmentAtUtc = successAt,
            LastSuccessfulSegmentName = segmentName,
            LastAttemptedSegmentAtUtc = attemptedAt,
            LagSeconds = lag,
            ArchivedCountLast24h = archived,
            FailedCountLast24h = failed,
            LastFailureReason = failureReason,
            HealthLevel = level,
            HealthReason = reason,
        };
    }

    private static (WalArchiveHealthLevel Level, string Reason) Classify(
        bool configured,
        DateTime? successAt,
        int? lag,
        int failed)
    {
        if (!configured)
            return (WalArchiveHealthLevel.Critical, DisabledReason);

        if (!successAt.HasValue)
            return (WalArchiveHealthLevel.Critical, NoSegmentReason);

        if (failed >= CriticalFailureCount)
            return (WalArchiveHealthLevel.Critical, FailuresReason);

        if (lag is null || lag >= CriticalLagSeconds)
            return (WalArchiveHealthLevel.Critical, LagCriticalReason);

        if (lag < HealthyLagExclusiveSeconds && failed == 0)
            return (WalArchiveHealthLevel.Healthy, HealthyReason);

        if (failed > 0 && lag < HealthyLagExclusiveSeconds)
            return (WalArchiveHealthLevel.Degraded, RecentFailuresReason);

        return (WalArchiveHealthLevel.Degraded, LagDegradedReason);
    }

    internal static string? SanitizeSegmentName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var file = name.Trim();
        if (file.Contains('/') || file.Contains('\\') || file.Contains(':'))
            file = Path.GetFileName(file.Replace('\\', '/'));

        if (string.IsNullOrWhiteSpace(file) || file.Contains("..", StringComparison.Ordinal))
            return null;

        return SafeSegmentName.IsMatch(file) ? file : null;
    }

    internal static string? SanitizeFailureReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        var token = reason.Trim();
        if (token.Length > 80 || !SafeFailureToken.IsMatch(token))
            return null;

        return token;
    }

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
