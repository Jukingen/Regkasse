namespace KasseAPI_Final.Models;

/// <summary>
/// Idempotent record of a provider webhook. Does not create fiscal <see cref="PaymentDetails"/>.
/// </summary>
public sealed class GatewayWebhookEvent
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>Internal <see cref="CardPaymentTransaction"/> id when matched.</summary>
    public Guid? IntentId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    public string? EventType { get; set; }

    public DateTime ReceivedAtUtc { get; set; }

    public bool Applied { get; set; }
}
