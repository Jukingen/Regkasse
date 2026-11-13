using KasseAPI_Final.Services.PaymentGateway;

namespace KasseAPI_Final.Models;

/// <summary>
/// POS hosted-payment flow statuses for DTOs. Persistence is
/// <see cref="CardPaymentTransaction"/> / <c>gateway_payment_intents</c> (not a separate table).
/// Webhooks may reach <see cref="GatewaySucceeded"/> only; fiscal COMPLETED is the POS
/// <c>POST /api/pos/payment</c> link of <c>PaymentId</c>, not a webhook target.
/// </summary>
public static class OnlinePaymentStatuses
{
    public const string Pending = "PENDING";
    public const string AwaitingPaymentGateway = "AWAITING_PAYMENT_GATEWAY";
    public const string GatewaySucceeded = "GATEWAY_SUCCEEDED";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Refunded = "REFUNDED";

    public static bool IsTerminal(string status) =>
        status is Completed or Failed or Refunded or GatewaySucceeded;

    public static bool CanTransition(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.Ordinal))
            return true;

        return (from, to) switch
        {
            (Pending, AwaitingPaymentGateway) => true,
            (Pending, GatewaySucceeded) => true,
            (Pending, Failed) => true,
            (AwaitingPaymentGateway, GatewaySucceeded) => true,
            (AwaitingPaymentGateway, Failed) => true,
            (GatewaySucceeded, Completed) => true,
            (GatewaySucceeded, Refunded) => true,
            (Completed, Refunded) => true,
            _ => false
        };
    }

    public static string FromGatewayIntentStatus(PaymentIntentStatus status) =>
        FromCardStatus(CardPaymentTransactionStatuses.FromPaymentIntentStatus(status));

    /// <summary>Maps persisted card-intent status to the POS/admin flow DTO status.</summary>
    public static string FromCardStatus(string status, Guid? paymentId = null) =>
        status switch
        {
            CardPaymentTransactionStatuses.Created => AwaitingPaymentGateway,
            CardPaymentTransactionStatuses.Pending => AwaitingPaymentGateway,
            CardPaymentTransactionStatuses.Succeeded when paymentId is Guid => Completed,
            CardPaymentTransactionStatuses.Succeeded => GatewaySucceeded,
            CardPaymentTransactionStatuses.Failed => Failed,
            CardPaymentTransactionStatuses.Cancelled => Failed,
            CardPaymentTransactionStatuses.Expired => "Expired",
            CardPaymentTransactionStatuses.Refunded => Refunded,
            _ => AwaitingPaymentGateway
        };

    public static string ToCardStatus(string flowStatus) =>
        flowStatus switch
        {
            GatewaySucceeded or Completed or "succeeded" or "success" or "paid"
                or "gateway_succeeded" =>
                CardPaymentTransactionStatuses.Succeeded,
            Failed or "fail" or "failure" or "cancelled" or "canceled" =>
                CardPaymentTransactionStatuses.Failed,
            Refunded or "refunded" => CardPaymentTransactionStatuses.Refunded,
            _ => CardPaymentTransactionStatuses.Pending
        };
}

public static class OnlinePaymentMethods
{
    public const string Card = "card";
    public const string PayPal = "paypal";

    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Card,
        PayPal,
        "credit_card"
    };

    public static string? Normalize(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
            return null;
        var trimmed = method.Trim().ToLowerInvariant();
        if (trimmed is "credit_card" or "card")
            return Card;
        if (trimmed == PayPal)
            return PayPal;
        return Allowed.Contains(trimmed) ? trimmed : null;
    }
}

public static class OnlinePaymentErrorCodes
{
    public const string InvalidAmount = "ONLINE_PAYMENT_INVALID_AMOUNT";
    public const string InvalidMethod = "ONLINE_PAYMENT_INVALID_METHOD";
    public const string InvalidRegister = "ONLINE_PAYMENT_INVALID_REGISTER";
    public const string InvalidReturnUrl = "ONLINE_PAYMENT_INVALID_RETURN_URL";
    public const string NotFound = "ONLINE_PAYMENT_NOT_FOUND";
    public const string GatewayError = "ONLINE_PAYMENT_GATEWAY_ERROR";
    public const string CreateFailed = "ONLINE_PAYMENT_CREATE_FAILED";
    public const string InvalidWebhook = "ONLINE_PAYMENT_INVALID_WEBHOOK";
    public const string UnknownProvider = "ONLINE_PAYMENT_UNKNOWN_PROVIDER";
    public const string InvalidOutcome = "ONLINE_PAYMENT_INVALID_OUTCOME";
    public const string IllegalTransition = "ONLINE_PAYMENT_ILLEGAL_TRANSITION";
    public const string TenantContextRequired = "TENANT_CONTEXT_REQUIRED";
    public const string GatewayRefundFailed = "GATEWAY_REFUND_FAILED";
}
