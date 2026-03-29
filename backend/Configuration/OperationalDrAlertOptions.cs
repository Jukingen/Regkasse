namespace KasseAPI_Final.Configuration;

/// <summary>
/// DR uyarıları için isteğe bağlı webhook; kapalıyken yalnızca log publisher çalışır.
/// </summary>
public sealed class OperationalDrAlertOptions
{
    public const string SectionName = "OperationalDr:Alerts";

    /// <summary>HTTP webhook POST; boş veya kapalıysa gönderim yapılmaz.</summary>
    public bool WebhookEnabled { get; set; }

    public string? WebhookUrl { get; set; }

    /// <summary>İstek iptali için üst sınır (1–120 sn).</summary>
    public int WebhookTimeoutSeconds { get; set; } = 15;

    /// <summary>Örn. Authorization veya X-Custom-Secret; ad doluysa değer header olarak eklenir.</summary>
    public string? WebhookSecretHeaderName { get; set; }

    public string? WebhookSecretHeaderValue { get; set; }
}
