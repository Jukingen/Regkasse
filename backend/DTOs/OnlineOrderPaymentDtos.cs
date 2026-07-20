namespace KasseAPI_Final.DTOs;

public sealed class CreateOnlineOrderPaymentIntentRequestDto
{
    /// <summary>Tenant slug required for anonymous/public checkout.</summary>
    public string? Tenant { get; init; }
}

public sealed class OnlineOrderPaymentIntentResponseDto
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public Guid? OrderId { get; init; }
    public string? OrderNumber { get; init; }
    public string? PaymentIntentId { get; init; }
    public string? ClientSecret { get; init; }
    public string? Provider { get; init; }
    public decimal? Amount { get; init; }
    public string Currency { get; init; } = "EUR";
}

public sealed class ConfirmOnlineOrderPaymentRequestDto
{
    public string PaymentIntentId { get; init; } = string.Empty;
    /// <summary>Optional Stripe payment method id or mock test card digits.</summary>
    public string? PaymentMethodId { get; init; }
}

public sealed class UpdateOnlineOrderStatusRequestDto
{
    public string Status { get; init; } = string.Empty;
}

public sealed class UpdateOnlineOrderStatusResponseDto
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public OnlineOrderDto? Order { get; init; }
}
