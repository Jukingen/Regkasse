using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Order;

public sealed class OnlineOrderPaymentResult
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public string? PaymentIntentId { get; init; }
    public string? ClientSecret { get; init; }
    public string? Provider { get; init; }
    public OnlineOrder? Order { get; init; }

    public static OnlineOrderPaymentResult Success(
        OnlineOrder order,
        string paymentIntentId,
        string? clientSecret,
        string provider) =>
        new()
        {
            Succeeded = true,
            Order = order,
            PaymentIntentId = paymentIntentId,
            ClientSecret = clientSecret,
            Provider = provider
        };

    public static OnlineOrderPaymentResult Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error
        };
}

public interface IOnlineOrderPaymentService
{
    /// <summary>Create Stripe/Mock PaymentIntent for an online order (amount = order.Total).</summary>
    Task<OnlineOrderPaymentResult> CreatePaymentIntentAsync(
        Guid onlineOrderId,
        string? tenantSlug = null,
        CancellationToken ct = default);

    /// <summary>Confirm intent with gateway and mark order paid when succeeded.</summary>
    Task<OnlineOrderPaymentResult> ConfirmPaymentAsync(
        string paymentIntentId,
        string? paymentMethodId = null,
        CancellationToken ct = default);

    /// <summary>Mark order paid from trusted webhook (no re-confirm if already paid).</summary>
    Task<OnlineOrderPaymentResult> MarkPaidFromGatewayAsync(
        string paymentIntentId,
        CancellationToken ct = default);
}
