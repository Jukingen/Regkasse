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
}
