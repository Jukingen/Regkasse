using System;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Immutable snapshot of the last TSE health probe (in-memory; refreshed by <see cref="TseHealthCheckService"/>).
/// </summary>
public sealed class TseHealthSnapshot
{
    public TseOperationalHealth Status { get; init; } = TseOperationalHealth.Degraded;

    /// <summary>UTC time of the last completed probe (success or failure).</summary>
    public DateTime? LastCheckUtc { get; init; }

    /// <summary>UTC time of the last probe that reported device connected and ready.</summary>
    public DateTime? LastSuccessfulPingUtc { get; init; }

    public int ConsecutiveFailures { get; init; }

    /// <summary>Last device error or probe exception message (safe for API exposure).</summary>
    public string? LastErrorMessageSafe { get; init; }

    /// <summary>
    /// Optimistic ETA when <see cref="Status"/> is <see cref="TseOperationalHealth.Offline"/> (next scheduled health check).
    /// </summary>
    public DateTime? EstimatedRecoveryTimeUtc { get; init; }

    public bool HasCompletedProbe => LastCheckUtc.HasValue;
}
