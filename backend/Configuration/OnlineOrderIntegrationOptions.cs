namespace KasseAPI_Final.Configuration;

/// <summary>
/// Per-integration webhook secrets. Keys are <c>{provider}:{externalLocationId}</c>.
/// Values come from user-secrets (Development) or the secret store / environment (Production).
/// Never commit a secret. An empty value is treated as missing.
/// </summary>
public sealed class OnlineOrderIntegrationOptions
{
    public const string SectionName = "OnlineOrderIntegrations";

    public Dictionary<string, string> WebhookSecrets { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public WoltIntegrationOptions Wolt { get; set; } = new();
}

/// <summary>
/// Wolt OAuth client. Secrets come from user-secrets or the environment.
/// <see cref="AuthorizationEndpoint"/> is the self-service start URL from the Wolt cabinet.
/// </summary>
public sealed class WoltIntegrationOptions
{
    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string? RedirectUri { get; set; }

    public string? AuthorizationEndpoint { get; set; }

    public string TokenUrl { get; set; } =
        "https://integrations-authentication-service.wolt.com/oauth2/token";

    public string OrderBaseUrl { get; set; } = "https://pos-integration-service.wolt.com";
}
