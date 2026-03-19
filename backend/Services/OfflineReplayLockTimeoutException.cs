using System;
using System.Collections.Generic;

namespace KasseAPI_Final.Services;

/// <summary>
/// Thrown when advisory lock could not be acquired within the configured max wait time (lock starvation / timeout).
/// Caller should audit, log, and return LOCK_TIMEOUT to client so they can retry.
/// </summary>
public sealed class OfflineReplayLockTimeoutException : Exception
{
    public int WaitDurationMs { get; }
    public IReadOnlyList<Guid> CashRegisterIds { get; }

    public OfflineReplayLockTimeoutException(int waitDurationMs, IReadOnlyList<Guid> cashRegisterIds)
        : base($"Advisory lock timeout after {waitDurationMs}ms for register(s) {string.Join(", ", cashRegisterIds)}.")
    {
        WaitDurationMs = waitDurationMs;
        CashRegisterIds = cashRegisterIds ?? Array.Empty<Guid>();
    }
}
