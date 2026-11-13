namespace KasseAPI_Final.DTOs;

/// <summary>
/// Deployment-wide business snapshot for Super Admin (<c>system.critical</c>).
/// Distinct from the Prometheus text exposition at <c>GET /metrics</c>.
/// </summary>
public sealed class PlatformBusinessMetricsDto
{
    public DateTime GeneratedAtUtc { get; init; }

    /// <summary>Mandant rows, excluding the platform sentinel and soft-deleted tenants.</summary>
    public int TotalTenants { get; init; }

    /// <summary>Distinct mandants with at least one receipt in the last 30 days.</summary>
    public int ActiveTenantsLast30Days { get; init; }

    public CountWindowDto ReceiptsIssued { get; init; } = new();

    /// <summary>Sum of <c>receipts.grand_total</c> in each window (EUR).</summary>
    public MoneyWindowDto RevenueEur { get; init; } = new();

    /// <summary>POS auth sessions that have not been revoked.</summary>
    public int ActivePosSessions { get; init; }

    /// <summary>
    /// FinanzOnline outbox rows in a failed state
    /// (retryable, protocol failure, permanent, dead letter, or manual review).
    /// </summary>
    public int FailedFinanzOnlineSubmissions { get; init; }
}

public sealed class CountWindowDto
{
    public int Last24Hours { get; init; }

    public int Last7Days { get; init; }

    public int Last30Days { get; init; }
}

public sealed class MoneyWindowDto
{
    public decimal Last24Hours { get; init; }

    public decimal Last7Days { get; init; }

    public decimal Last30Days { get; init; }
}
