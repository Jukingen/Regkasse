namespace KasseAPI_Final.DTOs;

public sealed class ServicePricingDto
{
    public required string ServiceId { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Tier { get; init; }
    public decimal PriceMonthly { get; init; }
    public decimal PriceYearly { get; init; }
    public required IReadOnlyList<string> Features { get; init; }
    public string Currency { get; init; } = "EUR";
}
