using System;
using KasseAPI_Final.Models;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Thread-safe in-memory store for TSE health snapshots.
/// </summary>
public sealed class TseHealthStateStore : ITseHealthMonitor
{
    private readonly Lock _lock = new();
    private TseHealthSnapshot _snapshot = new()
    {
        Status = TseOperationalHealth.Degraded,
        ConsecutiveFailures = 0
    };

    private readonly IOptionsMonitor<TseOptions> _options;

    public TseHealthStateStore(IOptionsMonitor<TseOptions> options)
    {
        _options = options;
    }

    public TseHealthSnapshot Snapshot
    {
        get
        {
            lock (_lock)
                return _snapshot;
        }
    }

    public event EventHandler<TseHealthChangedEventArgs>? StatusChanged;

    /// <summary>Called from <see cref="TseHealthCheckService"/> after each probe.</summary>
    public void ApplyProbeResult(bool pingSucceeded, string? errorSafe)
    {
        var opts = _options.CurrentValue;
        var intervalSec = Math.Clamp(opts.HealthCheckIntervalSeconds, 5, 600);
        var now = DateTime.UtcNow;

        TseHealthSnapshot next;
        lock (_lock)
        {
            var prev = _snapshot;
            var failures = pingSucceeded ? 0 : prev.ConsecutiveFailures + 1;
            var offlineThreshold = Math.Max(1, opts.OfflineAfterConsecutiveFailures);
            var degradedLower = Math.Max(1, opts.DegradedAfterConsecutiveFailures);

            var status = TseOperationalHealth.Online;
            if (!pingSucceeded)
            {
                if (failures >= offlineThreshold)
                    status = TseOperationalHealth.Offline;
                else if (failures >= degradedLower)
                    status = TseOperationalHealth.Degraded;
                else
                    status = TseOperationalHealth.Degraded;
            }

            DateTime? lastOk = pingSucceeded
                ? now
                : prev.LastSuccessfulPingUtc;

            DateTime? eta = null;
            if (status == TseOperationalHealth.Offline)
                eta = now.AddSeconds(intervalSec);

            next = new TseHealthSnapshot
            {
                Status = status,
                LastCheckUtc = now,
                LastSuccessfulPingUtc = lastOk,
                ConsecutiveFailures = failures,
                LastErrorMessageSafe = pingSucceeded ? null : errorSafe,
                EstimatedRecoveryTimeUtc = eta
            };

            var changed = prev.Status != next.Status;
            _snapshot = next;
            if (changed)
            {
                StatusChanged?.Invoke(this, new TseHealthChangedEventArgs { Previous = prev, Current = next });
            }
        }
    }
}
