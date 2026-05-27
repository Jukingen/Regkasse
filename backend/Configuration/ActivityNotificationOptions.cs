namespace KasseAPI_Final.Configuration;

/// <summary>
/// Optional outbound channels for admin activity notifications (email + Slack/Discord webhook).
/// </summary>
public sealed class ActivityNotificationOptions
{
    public const string SectionName = "ActivityNotifications";

    /// <summary>When false, no email is sent for activity events (in-app feed still works).</summary>
    public bool EmailEnabled { get; set; } = true;

    /// <summary>Comma/semicolon-separated fallback recipients when tenant settings have no admin email.</summary>
    public string? FallbackEmailRecipients { get; set; }

    public bool WebhookEnabled { get; set; }

    public string? WebhookUrl { get; set; }

    /// <summary>Generic (JSON), Slack (<c>{"text":...}</c>), or Discord (<c>{"content":...}</c>).</summary>
    public string WebhookFormat { get; set; } = "Generic";

    public int WebhookTimeoutSeconds { get; set; } = 15;

    public string? WebhookSecretHeaderName { get; set; }

    public string? WebhookSecretHeaderValue { get; set; }

    /// <summary>Minimum severity for email/webhook delivery (default: Warning).</summary>
    public string MinimumOutboundSeverity { get; set; } = "Warning";

    /// <summary>Offline intents per register before <see cref="ActivityEventType.OfflineQueueGrowing"/>.</summary>
    public int OfflineQueueAlertThreshold { get; set; } = 10;

    /// <summary>Background monitor interval in minutes.</summary>
    public int MonitorIntervalMinutes { get; set; } = 5;

    /// <summary>DELETE allowed only when event is older than this many days.</summary>
    public int DeleteRetentionDays { get; set; } = 90;

    /// <summary>Background purge removes events older than this many days (default 30).</summary>
    public int EventRetentionDays { get; set; } = 30;

    /// <summary>SSE keep-alive <c>ping</c> interval for <c>GET /api/admin/activities/stream</c>.</summary>
    public int SsePingIntervalSeconds { get; set; } = 30;
}
