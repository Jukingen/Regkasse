using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Known TSE integration webhook event type names.</summary>
public static class TseWebhookEventTypes
{
    public const string DeviceHealthChanged = "DeviceHealthChanged";
    public const string CertificateExpiry = "CertificateExpiry";
    public const string FailoverOccurred = "FailoverOccurred";
    public const string Test = "Test";

    public static readonly string[] All =
    {
        DeviceHealthChanged,
        CertificateExpiry,
        FailoverOccurred,
        Test,
    };

    public static bool IsKnown(string? eventType) =>
        !string.IsNullOrWhiteSpace(eventType)
        && All.Any(t => string.Equals(t, eventType.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string eventType) =>
        All.First(t => string.Equals(t, eventType.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class TseWebhookStatuses
{
    public const string Active = "Active";
    public const string Disabled = "Disabled";
    public const string Failing = "Failing";
}

/// <summary>Tenant-scoped TSE outbound webhook registration (integration delivery).</summary>
[Table("tse_webhooks")]
public sealed class TseWebhookRegistration
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(1024)]
    [Column("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>Comma-separated event type subscriptions.</summary>
    [Required]
    [MaxLength(512)]
    [Column("events")]
    public string EventsCsv { get; set; } = string.Empty;

    [MaxLength(256)]
    [Column("secret")]
    public string? Secret { get; set; }

    [Required]
    [MaxLength(24)]
    [Column("status")]
    public string Status { get; set; } = TseWebhookStatuses.Active;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(450)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("last_delivery_at")]
    public DateTime? LastDeliveryAt { get; set; }

    [Column("last_delivery_success")]
    public bool? LastDeliverySuccess { get; set; }

    [Column("consecutive_failures")]
    public int ConsecutiveFailures { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public ICollection<TseWebhookDelivery> Deliveries { get; set; } = new List<TseWebhookDelivery>();

    public IReadOnlyList<string> GetEventList() =>
        EventsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => e.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}

/// <summary>Append-only delivery / event log for a TSE webhook.</summary>
[Table("tse_webhook_deliveries")]
public sealed class TseWebhookDelivery
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("webhook_id")]
    public Guid WebhookId { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("event_id")]
    public Guid EventId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(64)]
    [Column("event_type")]
    public string EventType { get; set; } = string.Empty;

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    [Column("delivered_at")]
    public DateTime DeliveredAt { get; set; } = DateTime.UtcNow;

    [Column("success")]
    public bool Success { get; set; }

    [Column("http_status")]
    public int? HttpStatus { get; set; }

    [MaxLength(1000)]
    [Column("response_snippet")]
    public string? ResponseSnippet { get; set; }

    [MaxLength(2000)]
    [Column("payload_json")]
    public string PayloadJson { get; set; } = "{}";

    [ForeignKey(nameof(WebhookId))]
    public TseWebhookRegistration? Webhook { get; set; }
}
