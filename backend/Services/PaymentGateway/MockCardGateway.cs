using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.PaymentGateway;

/// <summary>
/// Development card gateway: in-memory payment intents with Stripe-compatible test card numbers.
/// </summary>
public sealed class MockCardGateway : IPaymentGateway
{
    private sealed class StoredIntent
    {
        public PaymentIntentResult Result { get; set; } = new();
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EUR";
    }

    private static readonly ConcurrentDictionary<Guid, StoredIntent> Intents = new();
    private static readonly Regex DigitsOnly = new(@"\D", RegexOptions.Compiled);

    /// <summary>Simulated decline amount for integration tests (e.g. 12.34 EUR).</summary>
    internal const decimal SimulatedDeclineAmount = 12.34m;

    private readonly ILogger<MockCardGateway> _logger;
    private readonly PaymentGatewayOptions _options;

    public MockCardGateway(
        ILogger<MockCardGateway> logger,
        IOptions<PaymentGatewayOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public string ProviderName => "Mock";

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(ResolveCreateDelayMs(), cancellationToken).ConfigureAwait(false);

        var paymentIntentId = request.InternalIntentId != Guid.Empty
            ? request.InternalIntentId
            : Guid.NewGuid();

        var result = new PaymentIntentResult
        {
            Success = true,
            PaymentIntentId = paymentIntentId.ToString("D"),
            ClientSecret = $"mock_secret_{paymentIntentId:N}",
            Status = PaymentIntentStatus.Created,
            TransactionId = $"MOCK_TXN_{DateTime.UtcNow:yyyyMMddHHmmss}_{Random.Shared.Next(10000, 99999)}"
        };

        Intents[paymentIntentId] = new StoredIntent
        {
            Result = CloneResult(result),
            Amount = request.Amount,
            Currency = request.Currency
        };

        _logger.LogInformation(
            "Mock card payment intent created: {PaymentIntentId} for {Amount} {Currency}",
            paymentIntentId,
            request.Amount,
            request.Currency);

        return CloneResult(result);
    }

    public async Task<PaymentIntentResult> ConfirmPaymentAsync(
        string gatewayPaymentIntentId,
        string? paymentMethodId,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(ResolveConfirmDelayMs(), cancellationToken).ConfigureAwait(false);

        if (!TryResolveIntentKey(gatewayPaymentIntentId, out var paymentIntentId)
            || !Intents.TryGetValue(paymentIntentId, out var stored))
        {
            _logger.LogWarning("Mock card payment confirm failed: intent {PaymentIntentId} not found", gatewayPaymentIntentId);
            return new PaymentIntentResult
            {
                Success = false,
                PaymentIntentId = gatewayPaymentIntentId,
                Status = PaymentIntentStatus.Failed,
                ErrorMessage = "Payment intent not found"
            };
        }

        if (stored.Result.Status is PaymentIntentStatus.Succeeded or PaymentIntentStatus.Cancelled or PaymentIntentStatus.Refunded)
            return CloneResult(stored.Result);

        bool success;
        string? error;
        string? brand;
        string? lastFour;

        if (!string.IsNullOrWhiteSpace(paymentMethodId))
        {
            var cardNumber = NormalizeCardNumber(paymentMethodId);
            (success, error, brand) = EvaluateTestCard(cardNumber);
            lastFour = cardNumber.Length >= 4 ? cardNumber[^4..] : null;
        }
        else if (stored.Amount == SimulatedDeclineAmount)
        {
            success = false;
            error = "Your card was declined.";
            brand = null;
            lastFour = null;
        }
        else
        {
            success = true;
            error = null;
            brand = "Visa";
            lastFour = "4242";
        }

        stored.Result.Success = success;
        stored.Result.Status = success ? PaymentIntentStatus.Succeeded : PaymentIntentStatus.Failed;
        stored.Result.ErrorMessage = error;
        stored.Result.CardBrand = brand;
        stored.Result.LastFourDigits = lastFour;
        stored.Result.PaymentIntentId = gatewayPaymentIntentId;
        if (success && string.IsNullOrWhiteSpace(stored.Result.TransactionId))
            stored.Result.TransactionId = $"MOCK_TXN_{DateTime.UtcNow:yyyyMMddHHmmss}_{Random.Shared.Next(10000, 99999)}";

        _logger.LogInformation(
            "Mock card payment confirmed: {PaymentIntentId} success={Success}",
            paymentIntentId,
            success);

        return CloneResult(stored.Result);
    }

    public async Task<PaymentIntentResult> CancelPaymentAsync(
        string gatewayPaymentIntentId,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(ResolveConfirmDelayMs(), cancellationToken).ConfigureAwait(false);

        if (!TryResolveIntentKey(gatewayPaymentIntentId, out var paymentIntentId)
            || !Intents.TryGetValue(paymentIntentId, out var stored))
        {
            return new PaymentIntentResult
            {
                Success = false,
                PaymentIntentId = gatewayPaymentIntentId,
                Status = PaymentIntentStatus.Failed,
                ErrorMessage = "Payment intent not found"
            };
        }

        if (stored.Result.Status == PaymentIntentStatus.Succeeded)
        {
            return new PaymentIntentResult
            {
                Success = false,
                PaymentIntentId = paymentIntentId.ToString("D"),
                Status = PaymentIntentStatus.Succeeded,
                ErrorMessage = "Cannot cancel a succeeded payment intent."
            };
        }

        stored.Result.Success = true;
        stored.Result.Status = PaymentIntentStatus.Cancelled;
        stored.Result.ErrorMessage = null;
        stored.Result.PaymentIntentId = gatewayPaymentIntentId;

        _logger.LogInformation("Mock card payment cancelled: {PaymentIntentId}", gatewayPaymentIntentId);

        return CloneResult(stored.Result);
    }

    public async Task<RefundResult> RefundPaymentAsync(
        string gatewayPaymentIntentId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(ResolveConfirmDelayMs(), cancellationToken).ConfigureAwait(false);

        if (amount <= 0m)
        {
            return new RefundResult
            {
                Success = false,
                ErrorMessage = "Refund amount must be greater than zero.",
                Status = PaymentIntentStatus.Failed
            };
        }

        if (!TryResolveIntentKey(gatewayPaymentIntentId, out var paymentIntentId)
            || !Intents.TryGetValue(paymentIntentId, out var stored)
            || stored.Result.Status != PaymentIntentStatus.Succeeded)
        {
            return new RefundResult
            {
                Success = false,
                ErrorMessage = "Payment intent is not refundable.",
                Status = PaymentIntentStatus.Failed
            };
        }

        stored.Result.Status = PaymentIntentStatus.Refunded;

        return new RefundResult
        {
            Success = true,
            RefundId = $"mock_ref_{Guid.NewGuid():N}"[..20],
            RefundedAmount = amount,
            Status = PaymentIntentStatus.Refunded
        };
    }

    public Task<PaymentIntentStatus> GetPaymentStatusAsync(
        string gatewayPaymentIntentId,
        CancellationToken cancellationToken = default)
    {
        if (TryResolveIntentKey(gatewayPaymentIntentId, out var paymentIntentId)
            && Intents.TryGetValue(paymentIntentId, out var stored))
            return Task.FromResult(stored.Result.Status);

        return Task.FromResult(PaymentIntentStatus.Failed);
    }

    private static bool TryResolveIntentKey(string gatewayPaymentIntentId, out Guid paymentIntentId) =>
        Guid.TryParse(gatewayPaymentIntentId, out paymentIntentId);

    internal static (bool Success, string? Error, string? Brand) EvaluateTestCard(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return (false, "Payment method id (card number) is required.", null);

        if (cardNumber.Length < 13 || cardNumber.Length > 19)
            return (false, "Invalid card number length.", null);

        return cardNumber switch
        {
            "4242424242424242" => (true, null, "Visa"),
            "5555555555554444" => (true, null, "Mastercard"),
            "4000000000000002" => (false, "Your card was declined.", "Visa"),
            "4000000000009995" => (false, "Your card has insufficient funds.", "Visa"),
            "4000000000003220" => (false, "Authentication required (3D Secure).", "Visa"),
            _ when cardNumber.StartsWith("4242", StringComparison.Ordinal) => (true, null, "Visa"),
            _ => (false, "Unsupported test card number. Use 4242424242424242 for success.", null)
        };
    }

    private static string NormalizeCardNumber(string? paymentMethodId) =>
        string.IsNullOrWhiteSpace(paymentMethodId)
            ? string.Empty
            : DigitsOnly.Replace(paymentMethodId, string.Empty);

    private int ResolveCreateDelayMs() =>
        _options.SimulateDelayMs > 0 ? _options.SimulateDelayMs : 500;

    private int ResolveConfirmDelayMs() =>
        _options.SimulateDelayMs > 0 ? _options.SimulateDelayMs : 300;

    private static PaymentIntentResult CloneResult(PaymentIntentResult source) =>
        new()
        {
            Success = source.Success,
            PaymentIntentId = source.PaymentIntentId,
            ClientSecret = source.ClientSecret,
            Status = source.Status,
            ErrorMessage = source.ErrorMessage,
            TransactionId = source.TransactionId,
            CardBrand = source.CardBrand,
            LastFourDigits = source.LastFourDigits
        };
}
