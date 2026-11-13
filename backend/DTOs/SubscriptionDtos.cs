using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class CreateSubscriptionRequestDto
{
    [Required]
    public Guid TenantId { get; init; }

    [Required]
    [MaxLength(64)]
    public string ServiceId { get; init; } = string.Empty;
}

public sealed class SubscriptionResponseDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public required string ServiceId { get; init; }
    public decimal Price { get; init; }
    public string Currency { get; init; } = "EUR";
    public required string Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime NextBillingDate { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
}

public sealed class SubscriptionMutationResponseDto
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public SubscriptionResponseDto? Subscription { get; init; }
}
