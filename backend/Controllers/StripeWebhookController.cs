using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Order;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Stripe webhook for online-order PaymentIntents (<c>payment_intent.succeeded</c>).
/// Uses <see cref="PaymentGatewayOptions.ResolveStripeWebhookSecret"/>.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/webhooks/stripe")]
public sealed class StripeWebhookController : ControllerBase
{
    private readonly IOnlineOrderPaymentService _payments;
    private readonly PaymentGatewayOptions _options;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        IOnlineOrderPaymentService payments,
        IOptions<PaymentGatewayOptions> options,
        ILogger<StripeWebhookController> logger)
    {
        _payments = payments;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Handle(CancellationToken ct)
    {
        string json;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            json = await reader.ReadToEndAsync(ct);

        var webhookSecret = _options.ResolveStripeWebhookSecret();
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            _logger.LogWarning("Stripe webhook received but PaymentGateway webhook secret is not configured.");
            return BadRequest(new { message = "Webhook secret is not configured." });
        }

        Event stripeEvent;
        try
        {
            var signature = Request.Headers["Stripe-Signature"].ToString();
            stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return BadRequest(new { message = "Invalid Stripe signature." });
        }

        // Stripe event type constant (Stripe.net EventTypes may vary by package version).
        if (string.Equals(stripeEvent.Type, "payment_intent.succeeded", StringComparison.Ordinal))
        {
            if (stripeEvent.Data.Object is PaymentIntent intent
                && !string.IsNullOrWhiteSpace(intent.Id))
            {
                var purpose = intent.Metadata != null
                              && intent.Metadata.TryGetValue("purpose", out var p)
                    ? p
                    : null;

                if (string.Equals(purpose, "online_order", StringComparison.OrdinalIgnoreCase)
                    || (intent.Metadata?.ContainsKey("online_order_id") ?? false))
                {
                    var result = await _payments.MarkPaidFromGatewayAsync(intent.Id, ct);
                    if (!result.Succeeded)
                    {
                        _logger.LogWarning(
                            "Stripe webhook could not mark online order paid for {PaymentIntentId}: {Code} {Error}",
                            intent.Id,
                            result.Code,
                            result.Error);
                    }
                }
            }
        }

        return Ok(new { received = true });
    }
}
