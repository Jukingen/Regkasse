namespace KasseAPI_Final.DTOs;

public sealed class CustomerDigitalServiceDto
{
    public Guid Id { get; init; }
    public required string ServiceId { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Tier { get; init; }
    public decimal Price { get; init; }
    public string Currency { get; init; } = "EUR";
    public required string Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime NextBillingDate { get; init; }
    public string? Url { get; init; }
}

public sealed class MenuSyncResponseDto
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? AppUrl { get; init; }
}
