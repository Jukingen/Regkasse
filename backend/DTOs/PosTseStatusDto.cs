namespace KasseAPI_Final.DTOs;

/// <summary>Cashier-facing TSE indicator values for <c>GET /api/pos/tse/status</c>.</summary>
public static class PosTseIndicatorStatuses
{
    public const string Active = "Active";
    public const string Degraded = "Degraded";
    public const string Inactive = "Inactive";
}

/// <summary>
/// POS TSE status for the header indicator. SIGN AT SCU is exposed as <see cref="ScuId"/>
/// (German TSS analog). Does not replace RKSV compact JWS signing.
/// </summary>
public sealed class PosTseStatusDto
{
    /// <summary>Active | Degraded | Inactive</summary>
    public string Status { get; init; } = PosTseIndicatorStatuses.Degraded;

    public string Message { get; init; } = string.Empty;

    public DateTime? LastCheck { get; init; }

    /// <summary>Fiskaly SIGN AT Signature Creation Unit id (UUIDv4).</summary>
    public string? ScuId { get; init; }

    /// <summary>Alias of <see cref="ScuId"/> for clients that still say TSS.</summary>
    public string? TssId { get; init; }

    public DateTime? CertificateValidUntil { get; init; }

    /// <summary>True when the live probe failed and the last successful ping is being used.</summary>
    public bool Cached { get; init; }

    /// <summary>Online | Degraded | Offline — process health used by POS offline routing.</summary>
    public string OperationalHealth { get; init; } = "Degraded";

    public string? LastErrorMessageSafe { get; init; }

    public int? NonFiscalPendingQueueCount { get; init; }

    public DateTime? EstimatedRecoveryTimeUtc { get; init; }

    public DateTime? LastSuccessfulPingUtc { get; init; }
}
