namespace KasseAPI_Final.DTOs;

/// <summary>Super Admin payment gateway status — never includes secrets.</summary>
public sealed class PaymentGatewaySettingsDto
{
    public string Provider { get; init; } = "Mock";
    public bool IsStripeProvider { get; init; }
    public bool ApiKeyConfigured { get; init; }
    public bool WebhookSecretConfigured { get; init; }
    public bool RequireCardIntentForPosPayments { get; init; }
    /// <summary>Fixed route path (prefix with API host in FA).</summary>
    public string WebhookPath { get; init; } = "/api/webhooks/stripe";
    /// <summary>Online checkout methods for website/PWA (card, paypal, bank, cash, online).</summary>
    public IReadOnlyList<string> OnlinePaymentMethods { get; init; } = Array.Empty<string>();
}

public sealed class UpdatePaymentGatewaySettingsRequestDto
{
    /// <summary>Allowed: card, paypal, bank, cash, online.</summary>
    public IReadOnlyList<string>? OnlinePaymentMethods { get; init; }
}
