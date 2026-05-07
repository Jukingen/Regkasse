using System;

namespace KasseAPI_Final.DTOs;

/// <summary>Cached TSE connectivity snapshot for POS dashboards.</summary>
public sealed class TseHealthResponseDto
{
    /// <summary>Online | Degraded | Offline</summary>
    public string Status { get; set; } = "Degraded";

    public DateTime? LastCheckUtc { get; set; }

    public DateTime? LastSuccessfulPingUtc { get; set; }

    public int ConsecutiveFailures { get; set; }

    /// <summary>Optional ETA when status is Offline (next probe).</summary>
    public DateTime? EstimatedRecoveryTimeUtc { get; set; }

    public string? LastErrorMessageSafe { get; set; }

    /// <summary>Count of server-side non-fiscal pending intents for the register (when cashRegisterId query was sent).</summary>
    public int? NonFiscalPendingQueueCount { get; set; }
}
