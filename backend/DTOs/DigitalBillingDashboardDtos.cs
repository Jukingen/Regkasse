namespace KasseAPI_Final.DTOs;

/// <summary>
/// Super Admin digital-service revenue snapshot (MRR from active subscription price snapshots).
/// Not license_sales / not fiscal revenue.
/// </summary>
public sealed class DigitalBillingDashboardDto
{
    /// <summary>Sum of active subscription monthly prices (MRR).</summary>
    public decimal Total { get; init; }

    /// <summary>Active MRR for website-type services.</summary>
    public decimal Websites { get; init; }

    /// <summary>Active MRR for app-type services.</summary>
    public decimal Apps { get; init; }

    /// <summary>Count of active subscriptions.</summary>
    public int Subscribers { get; init; }

    public string Currency { get; init; } = "EUR";

    public IReadOnlyList<DigitalBillingSubscriptionRowDto> Subscriptions { get; init; } =
        Array.Empty<DigitalBillingSubscriptionRowDto>();
}

public sealed class DigitalBillingSubscriptionRowDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public required string Tenant { get; init; }
    public required string Service { get; init; }
    public required string ServiceId { get; init; }
    public decimal Price { get; init; }
    public string Currency { get; init; } = "EUR";
    public DateTime StartDate { get; init; }
    public DateTime NextBilling { get; init; }
    public required string Status { get; init; }
}
