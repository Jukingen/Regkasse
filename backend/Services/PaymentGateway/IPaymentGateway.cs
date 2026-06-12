namespace KasseAPI_Final.Services.PaymentGateway;

public interface IPaymentGateway
{
    string ProviderName { get; }

    Task<PaymentIntentResult> CreatePaymentIntentAsync(
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentIntentResult> ConfirmPaymentAsync(
        string gatewayPaymentIntentId,
        string? paymentMethodId,
        CancellationToken cancellationToken = default);

    Task<PaymentIntentResult> CancelPaymentAsync(
        string gatewayPaymentIntentId,
        CancellationToken cancellationToken = default);

    Task<RefundResult> RefundPaymentAsync(
        string gatewayPaymentIntentId,
        decimal amount,
        CancellationToken cancellationToken = default);

    Task<PaymentIntentStatus> GetPaymentStatusAsync(
        string gatewayPaymentIntentId,
        CancellationToken cancellationToken = default);
}

public sealed class CreatePaymentIntentRequest
{
    public Guid InternalIntentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? PaymentMethodId { get; set; }
    public string? CustomerId { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class PaymentIntentResult
{
    public bool Success { get; set; }
    public string PaymentIntentId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public PaymentIntentStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TransactionId { get; set; }
    public string? CardBrand { get; set; }
    public string? LastFourDigits { get; set; }
}

public sealed class RefundResult
{
    public bool Success { get; set; }
    public string? RefundId { get; set; }
    public decimal RefundedAmount { get; set; }
    public PaymentIntentStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum PaymentIntentStatus
{
    Created,
    Pending,
    Succeeded,
    Failed,
    Cancelled,
    Refunded
}
