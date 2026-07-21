namespace KasseAPI_Final.Models;

/// <summary>Tenant-scoped notification channels and per-event-type rules (FA admin).</summary>
public sealed class NotificationConfig
{
    public bool InAppEnabled { get; set; } = true;

    public bool EmailEnabled { get; set; }

    public List<string> EmailRecipients { get; set; } = [];

    public bool WebhookEnabled { get; set; }

    public string? WebhookUrl { get; set; }

    public string? WebhookSecret { get; set; }

    /// <summary>When empty, all event types are enabled.</summary>
    public Dictionary<ActivityEventType, bool> EnabledEvents { get; set; } = new();

    /// <summary>Minimum severity per event type (Info, Warning, Error, Critical). Omitted types use event default only.</summary>
    public Dictionary<ActivityEventType, string> SeverityThreshold { get; set; } = new();

    public static NotificationConfig CreateDefault() => new();
}
