using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services;

/// <summary>
/// Offline queue metrics for a reporting period (point-in-time snapshots + transitions).
/// </summary>
internal static class OfflineRecoveryMetricsCalculator
{
    internal static OfflineRecoveryReportDto Build(
        DateTime periodStartLocal,
        DateTime periodEndLocal,
        DateTime fromUtc,
        DateTime endBoundUtc,
        bool endExclusive,
        IReadOnlyList<OfflineTransaction> cohort,
        IReadOnlyDictionary<Guid, string> registerNumbers,
        int recentLimit = 50)
    {
        var endInstant = endBoundUtc;
        var periodRows = cohort
            .Where(x => TouchesPeriod(x, fromUtc, endInstant, endExclusive))
            .ToList();

        var pendingAtStart = cohort.Count(x => WasPendingAt(x, fromUtc));
        var pendingAtEnd = cohort.Count(x => IsPendingAt(x, endInstant, endExclusive));

        var syncedInPeriod = periodRows
            .Where(x => x.Status == OfflineTransactionStatus.Synced
                        && x.FiscalizedAtUtc.HasValue
                        && InPeriod(x.FiscalizedAtUtc.Value, fromUtc, endInstant, endExclusive))
            .ToList();

        var recoveredSuccessfully = syncedInPeriod.Count(x => x.RetryCount <= 0);
        var recoveredWithRetry = syncedInPeriod.Count(x => x.RetryCount > 0);

        var permanentlyFailed = periodRows.Count(x =>
            x.Status == OfflineTransactionStatus.Failed
            && (!x.FiscalizedAtUtc.HasValue || x.FiscalizedAtUtc.Value >= fromUtc));

        var manuallyIntervened = periodRows.Count(x =>
            x.RetryCount > 0
            && (x.Status == OfflineTransactionStatus.Failed
                || (x.Status == OfflineTransactionStatus.Synced
                    && x.FiscalizedAtUtc.HasValue
                    && InPeriod(x.FiscalizedAtUtc.Value, fromUtc, endInstant, endExclusive))));

        var recoverySeconds = syncedInPeriod
            .Where(x => x.FiscalizedAtUtc.HasValue)
            .Select(x => (x.FiscalizedAtUtc!.Value - x.ServerReceivedAtUtc).TotalSeconds)
            .Where(s => s >= 0)
            .ToList();

        var averageRecoverySeconds = recoverySeconds.Count > 0 ? recoverySeconds.Average() : 0;
        var maxRecoverySeconds = recoverySeconds.Count > 0 ? recoverySeconds.Max() : 0;

        var byRegister = cohort
            .GroupBy(x => x.CashRegisterId)
            .Select(g =>
            {
                registerNumbers.TryGetValue(g.Key, out var regNum);
                return new OfflineRecoveryRegisterBreakdownDto
                {
                    CashRegisterId = g.Key,
                    RegisterNumber = regNum ?? g.Key.ToString(),
                    PendingCount = g.Count(x => IsPendingAt(x, endInstant, endExclusive)),
                    FailedCount = g.Count(x => x.Status == OfflineTransactionStatus.Failed),
                };
            })
            .OrderBy(x => x.RegisterNumber)
            .ToList();

        var recent = periodRows
            .OrderByDescending(x => x.ServerReceivedAtUtc)
            .Take(Math.Clamp(recentLimit, 1, 200))
            .Select(x => new OfflineRecoveryRowDto
            {
                Id = x.Id,
                CashRegisterId = x.CashRegisterId,
                Status = x.Status.ToString(),
                ServerReceivedAtUtc = x.ServerReceivedAtUtc,
                LastReplayAttemptAt = x.LastReplayAttemptAt,
                LastError = x.LastErrorMessageSafe,
                ClockDriftWarning = x.ClockDriftWarning,
                SequenceGapDetected = x.SequenceGapDetected,
                RetryCount = x.RetryCount,
            })
            .ToList();

        var lastReplay = cohort
            .Where(x => x.LastReplayAttemptAt.HasValue)
            .MaxBy(x => x.LastReplayAttemptAt)?.LastReplayAttemptAt;

        return new OfflineRecoveryReportDto
        {
            PeriodStartLocal = periodStartLocal,
            PeriodEndLocal = periodEndLocal,
            PendingAtStart = pendingAtStart,
            PendingAtEnd = pendingAtEnd,
            RecoveredSuccessfully = recoveredSuccessfully,
            RecoveredWithRetry = recoveredWithRetry,
            PermanentlyFailed = permanentlyFailed,
            ManuallyIntervened = manuallyIntervened,
            AverageRecoverySeconds = averageRecoverySeconds,
            MaxRecoverySeconds = maxRecoverySeconds,
            ByRegister = byRegister,
            PendingCount = pendingAtEnd,
            FailedCount = permanentlyFailed,
            CompletedCount = recoveredSuccessfully + recoveredWithRetry,
            ClockDriftWarningCount = periodRows.Count(x => x.ClockDriftWarning),
            SequenceGapCount = periodRows.Count(x => x.SequenceGapDetected),
            LastReplayAtUtc = lastReplay,
            RecentRows = recent,
            OperatorNoteDe =
                "Warteschlangen-Snapshots aus OfflineTransaction-Status (kein separates Status-Historien-Log). "
                + "RetryCount umfasst automatische und manuelle Wiederholungen.",
        };
    }

    private static bool TouchesPeriod(OfflineTransaction x, DateTime fromUtc, DateTime endUtc, bool endExclusive)
    {
        if (InPeriod(x.ServerReceivedAtUtc, fromUtc, endUtc, endExclusive))
            return true;
        if (x.FiscalizedAtUtc.HasValue && InPeriod(x.FiscalizedAtUtc.Value, fromUtc, endUtc, endExclusive))
            return true;
        if (x.LastReplayAttemptAt.HasValue && InPeriod(x.LastReplayAttemptAt.Value, fromUtc, endUtc, endExclusive))
            return true;
        return WasPendingAt(x, fromUtc) && IsPendingAt(x, endUtc, endExclusive);
    }

    private static bool WasPendingAt(OfflineTransaction x, DateTime atUtc)
    {
        if (x.ServerReceivedAtUtc >= atUtc)
            return false;

        if (x.FiscalizedAtUtc.HasValue && x.FiscalizedAtUtc.Value < atUtc)
            return false;

        if (x.Status is OfflineTransactionStatus.Pending or OfflineTransactionStatus.NonFiscalPending)
            return true;

        if (x.Status == OfflineTransactionStatus.Synced && x.FiscalizedAtUtc >= atUtc)
            return true;

        if (x.Status == OfflineTransactionStatus.Failed)
        {
            if (!x.LastReplayAttemptAt.HasValue || x.LastReplayAttemptAt.Value >= atUtc)
                return true;
        }

        return false;
    }

    private static bool IsPendingAt(OfflineTransaction x, DateTime endUtc, bool endExclusive)
    {
        if (endExclusive && x.ServerReceivedAtUtc >= endUtc)
            return false;
        if (!endExclusive && x.ServerReceivedAtUtc > endUtc)
            return false;

        return x.Status is OfflineTransactionStatus.Pending or OfflineTransactionStatus.NonFiscalPending;
    }

    private static bool InPeriod(DateTime instant, DateTime fromUtc, DateTime endUtc, bool endExclusive) =>
        endExclusive
            ? instant >= fromUtc && instant < endUtc
            : instant >= fromUtc && instant <= endUtc;
}
