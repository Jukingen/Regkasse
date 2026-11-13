namespace KasseAPI_Final.Services.Email;

/// <summary>Customer-facing online order confirmation / status emails (SMTP).</summary>
public interface IOnlineOrderCustomerEmailService
{
    bool IsConfigured { get; }

    Task<bool> TrySendOrderConfirmationAsync(
        OnlineOrderCustomerEmailRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> TrySendOrderStatusAsync(
        OnlineOrderCustomerEmailRequest request,
        string statusHeadline,
        string statusBody,
        CancellationToken cancellationToken = default);
}

public sealed record OnlineOrderCustomerEmailRequest(
    string ToEmail,
    string CustomerName,
    string OrderNumber,
    decimal Total,
    string Currency,
    string OrderType,
    int EstimatedMinutes);
