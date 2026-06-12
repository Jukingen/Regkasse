using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;
using Stripe;

namespace KasseAPI_Final.Services.PaymentGateway;

/// <summary>
/// Production Stripe card gateway template. Requires <see cref="PaymentGatewayOptions.ResolveStripeApiKey"/>.
/// </summary>
public sealed class StripeCardGateway : IPaymentGateway
{
    private readonly IStripeClient _stripeClient;
    private readonly PaymentGatewayOptions _options;
    private readonly ILogger<StripeCardGateway> _logger;

    public StripeCardGateway(
        IStripeClient stripeClient,
        IOptions<PaymentGatewayOptions> options,
        ILogger<StripeCardGateway> logger)
    {
        _stripeClient = stripeClient;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "Stripe";

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var metadata = new Dictionary<string, string>(request.Metadata)
        {
            ["internal_intent_id"] = request.InternalIntentId.ToString("D")
        };

        var options = new PaymentIntentCreateOptions
        {
            Amount = StripePaymentIntentStatusMapper.ToStripeAmount(request.Amount),
            Currency = request.Currency.ToLowerInvariant(),
            PaymentMethodTypes = ["card"],
            Description = request.Description,
            Metadata = metadata
        };

        if (!string.IsNullOrWhiteSpace(request.CustomerId))
            options.Customer = request.CustomerId;

        try
        {
            var service = new PaymentIntentService(_stripeClient);
            var intent = await service.CreateAsync(options, cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Stripe payment intent created: {PaymentIntentId} amount={Amount} currency={Currency}",
                intent.Id,
                request.Amount,
                request.Currency);

            return StripePaymentIntentStatusMapper.ToResult(intent);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe CreatePaymentIntent failed");
            return FailedResult(ex, gatewayPaymentIntentId: null);
        }
    }

    public async Task<PaymentIntentResult> ConfirmPaymentAsync(
        string gatewayPaymentIntentId,
        string? paymentMethodId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(gatewayPaymentIntentId))
            return MissingIntentResult(string.Empty);

        try
        {
            var service = new PaymentIntentService(_stripeClient);
            PaymentIntent intent;

            if (!string.IsNullOrWhiteSpace(paymentMethodId))
            {
                var confirmOptions = new PaymentIntentConfirmOptions
                {
                    PaymentMethod = paymentMethodId
                };
                intent = await service.ConfirmAsync(
                    gatewayPaymentIntentId,
                    confirmOptions,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                intent = await service.GetAsync(gatewayPaymentIntentId, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            var result = StripePaymentIntentStatusMapper.ToResult(intent);
            result.Success = result.Status == PaymentIntentStatus.Succeeded;

            _logger.LogInformation(
                "Stripe payment intent confirmed: {PaymentIntentId} status={Status}",
                gatewayPaymentIntentId,
                intent.Status);

            return result;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe ConfirmPayment failed for {PaymentIntentId}", gatewayPaymentIntentId);
            return FailedResult(ex, gatewayPaymentIntentId);
        }
    }

    public async Task<PaymentIntentResult> CancelPaymentAsync(
        string gatewayPaymentIntentId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(gatewayPaymentIntentId))
            return MissingIntentResult(string.Empty);

        try
        {
            var service = new PaymentIntentService(_stripeClient);
            var intent = await service.CancelAsync(gatewayPaymentIntentId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Stripe payment intent cancelled: {PaymentIntentId}", gatewayPaymentIntentId);

            var result = StripePaymentIntentStatusMapper.ToResult(intent);
            result.Success = result.Status == PaymentIntentStatus.Cancelled;
            return result;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe CancelPayment failed for {PaymentIntentId}", gatewayPaymentIntentId);
            return FailedResult(ex, gatewayPaymentIntentId);
        }
    }

    public async Task<RefundResult> RefundPaymentAsync(
        string gatewayPaymentIntentId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (amount <= 0m)
        {
            return new RefundResult
            {
                Success = false,
                ErrorMessage = "Refund amount must be greater than zero.",
                Status = PaymentIntentStatus.Failed
            };
        }

        if (string.IsNullOrWhiteSpace(gatewayPaymentIntentId))
        {
            return new RefundResult
            {
                Success = false,
                ErrorMessage = "Payment intent id is required.",
                Status = PaymentIntentStatus.Failed
            };
        }

        try
        {
            var options = new RefundCreateOptions
            {
                PaymentIntent = gatewayPaymentIntentId,
                Amount = StripePaymentIntentStatusMapper.ToStripeAmount(amount)
            };

            var refund = await new RefundService(_stripeClient)
                .CreateAsync(options, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Stripe refund created: {RefundId} for intent {PaymentIntentId}",
                refund.Id,
                gatewayPaymentIntentId);

            return new RefundResult
            {
                Success = true,
                RefundId = refund.Id,
                RefundedAmount = amount,
                Status = PaymentIntentStatus.Refunded
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe RefundPayment failed for {PaymentIntentId}", gatewayPaymentIntentId);
            return new RefundResult
            {
                Success = false,
                ErrorMessage = ex.StripeError?.Message ?? ex.Message,
                Status = PaymentIntentStatus.Failed
            };
        }
    }

    public async Task<PaymentIntentStatus> GetPaymentStatusAsync(
        string gatewayPaymentIntentId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(gatewayPaymentIntentId))
            return PaymentIntentStatus.Failed;

        try
        {
            var service = new PaymentIntentService(_stripeClient);
            var intent = await service.GetAsync(gatewayPaymentIntentId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return StripePaymentIntentStatusMapper.Map(intent.Status);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe GetPaymentStatus failed for {PaymentIntentId}", gatewayPaymentIntentId);
            return PaymentIntentStatus.Failed;
        }
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ResolveStripeApiKey()))
        {
            throw new InvalidOperationException(
                "Stripe card gateway is not configured. Set PaymentGateway:Stripe:ApiKey or use PaymentGateway:Provider=Mock.");
        }
    }

    private static PaymentIntentResult MissingIntentResult(string gatewayPaymentIntentId) =>
        new()
        {
            Success = false,
            PaymentIntentId = gatewayPaymentIntentId,
            Status = PaymentIntentStatus.Failed,
            ErrorMessage = "Payment intent id is required."
        };

    private static PaymentIntentResult FailedResult(StripeException ex, string? gatewayPaymentIntentId) =>
        new()
        {
            Success = false,
            PaymentIntentId = gatewayPaymentIntentId ?? string.Empty,
            Status = PaymentIntentStatus.Failed,
            ErrorMessage = ex.StripeError?.Message ?? ex.Message
        };
}
