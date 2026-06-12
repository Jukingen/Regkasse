namespace KasseAPI_Final.Configuration;

/// <summary>Stripe credentials under <see cref="PaymentGatewayOptions.Stripe"/>.</summary>
public sealed class PaymentGatewayStripeOptions
{
    public string? ApiKey { get; set; }
    public string? WebhookSecret { get; set; }
}

/// <summary>Card payment gateway provider selection (Mock for dev/simulation, Stripe for production).</summary>
public sealed class PaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway";

    /// <summary>Gateway provider: <c>Mock</c> (default) or <c>Stripe</c>.</summary>
    public string Provider { get; set; } = "Mock";

    /// <summary>When true, POS card payments must include a confirmed <see cref="DTOs.PaymentMethodRequest.CardPaymentIntentId"/>.</summary>
    public bool RequireCardIntentForPosPayments { get; set; }

    /// <summary>Simulated network delay for Mock gateway (milliseconds).</summary>
    public int SimulateDelayMs { get; set; }

    /// <summary>Nested Stripe settings (<c>PaymentGateway:Stripe:ApiKey</c>).</summary>
    public PaymentGatewayStripeOptions Stripe { get; set; } = new();

    /// <summary>Legacy flat key; prefer <see cref="PaymentGatewayStripeOptions.ApiKey"/>.</summary>
    public string? StripeSecretKey { get; set; }

    /// <summary>Legacy flat key; prefer <see cref="PaymentGatewayStripeOptions.WebhookSecret"/>.</summary>
    public string? StripeWebhookSecret { get; set; }

    public bool IsStripeProvider =>
        string.Equals(Provider, "Stripe", StringComparison.OrdinalIgnoreCase);

    public string? ResolveStripeApiKey()
    {
        if (!string.IsNullOrWhiteSpace(Stripe.ApiKey))
            return Stripe.ApiKey.Trim();
        if (!string.IsNullOrWhiteSpace(StripeSecretKey))
            return StripeSecretKey.Trim();
        return null;
    }

    public string? ResolveStripeWebhookSecret()
    {
        if (!string.IsNullOrWhiteSpace(Stripe.WebhookSecret))
            return Stripe.WebhookSecret.Trim();
        if (!string.IsNullOrWhiteSpace(StripeWebhookSecret))
            return StripeWebhookSecret.Trim();
        return null;
    }
}
