using KasseAPI_Final.DTOs;
using Microsoft.AspNetCore.Http;

namespace KasseAPI_Final.Services;

/// <summary>
/// Orchestrates provider-agnostic online payments (POS initiate, webhook apply, admin test).
/// Persistence is <c>gateway_payment_intents</c> (<see cref="Models.CardPaymentTransaction"/>).
/// Does not create fiscal <c>PaymentDetails</c>.
/// </summary>
public interface IPaymentGatewayService
{
    /// <summary>Starts an online payment: validates register, creates gateway intent, persists <c>gateway_payment_intents</c>.</summary>
    Task<PaymentGatewayOperationResult> ProcessPaymentAsync(
        InitiateOnlinePaymentRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>Alias of <see cref="ProcessPaymentAsync"/>.</summary>
    Task<PaymentGatewayOperationResult> CreateIntentAsync(
        InitiateOnlinePaymentRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>Polls intent state. Does not create a fiscal receipt.</summary>
    Task<PaymentGatewayOperationResult> ConfirmAsync(
        Guid onlinePaymentId,
        CancellationToken cancellationToken = default);

    /// <summary>Acquirer refund for a captured intent. Does not write fiscal <c>payment_details</c>.</summary>
    Task<PaymentGatewayOperationResult> RefundAsync(
        Guid onlinePaymentId,
        decimal amount,
        CancellationToken cancellationToken = default);

    /// <summary>Verifies provider signature and applies the event to the matching intent. Never creates fiscal rows.</summary>
    Task<PaymentGatewayWebhookResult> VerifyWebhookAsync(
        string provider,
        string payload,
        IHeaderDictionary headers,
        CancellationToken cancellationToken = default);

    /// <summary>Alias of <see cref="VerifyWebhookAsync"/>.</summary>
    Task<PaymentGatewayWebhookResult> HandleWebhookAsync(
        string provider,
        string payload,
        IHeaderDictionary headers,
        CancellationToken cancellationToken = default);

    /// <summary>Admin test-console: simulate gateway success or failure without calling the provider.</summary>
    Task<PaymentGatewayOperationResult> SimulateTestOutcomeAsync(
        SimulateOnlinePaymentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Tenant-scoped lookup; missing or cross-tenant rows return not-found.</summary>
    Task<PaymentGatewayOperationResult> GetByIdAsync(
        Guid onlinePaymentId,
        CancellationToken cancellationToken = default);
}

public sealed class PaymentGatewayOperationResult
{
    public bool Ok { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public OnlinePaymentDto? Payment { get; init; }

    public bool IsNotFound =>
        string.Equals(ErrorCode, Models.OnlinePaymentErrorCodes.NotFound, StringComparison.Ordinal);

    public static PaymentGatewayOperationResult Success(OnlinePaymentDto payment) =>
        new() { Ok = true, Payment = payment };

    public static PaymentGatewayOperationResult Fail(string code, string message) =>
        new() { Ok = false, ErrorCode = code, ErrorMessage = message };
}

public sealed class PaymentGatewayWebhookResult
{
    public bool SignatureValid { get; init; }
    public bool Applied { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public OnlinePaymentDto? Payment { get; init; }

    public static PaymentGatewayWebhookResult Invalid(string code, string message) =>
        new() { SignatureValid = false, ErrorCode = code, ErrorMessage = message };

    public static PaymentGatewayWebhookResult Ignored() =>
        new() { SignatureValid = true, Applied = false };

    public static PaymentGatewayWebhookResult AppliedOk(OnlinePaymentDto? payment) =>
        new() { SignatureValid = true, Applied = payment != null, Payment = payment };
}
