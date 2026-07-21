using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Activity;

public interface IActivityEventWebhookNotifier
{
    Task TrySendAsync(
        ActivityEvent evt,
        NotificationConfig tenantConfig,
        CancellationToken cancellationToken = default);
}

/// <summary>Slack/Discord/generic webhook for activity events.</summary>
public sealed class ActivityEventWebhookNotifier : IActivityEventWebhookNotifier
{
    public const string HttpClientName = "ActivityNotificationWebhook";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<ActivityNotificationOptions> _options;
    private readonly ILogger<ActivityEventWebhookNotifier> _logger;

    public ActivityEventWebhookNotifier(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<ActivityNotificationOptions> options,
        ILogger<ActivityEventWebhookNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task TrySendAsync(
        ActivityEvent evt,
        NotificationConfig tenantConfig,
        CancellationToken cancellationToken = default)
    {
        if (!NotificationConfigEvaluator.ShouldDeliverWebhook(tenantConfig, evt.Type, evt.Severity))
            return;

        var hostDefaults = _options.CurrentValue;
        var webhookUrl = tenantConfig.WebhookUrl?.Trim();
        if (string.IsNullOrEmpty(webhookUrl))
            return;

        var text = string.IsNullOrWhiteSpace(evt.Description)
            ? $"**{evt.Title}** ({evt.Type}, {evt.Severity})"
            : $"**{evt.Title}**\n{evt.Description}";

        var payload = BuildPayload(hostDefaults.WebhookFormat, text, evt);
        var timeoutSec = Math.Clamp(hostDefaults.WebhookTimeoutSeconds, 1, 120);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl);
            if (!string.IsNullOrWhiteSpace(tenantConfig.WebhookSecret))
            {
                request.Headers.TryAddWithoutValidation("X-Activity-Webhook-Secret", tenantConfig.WebhookSecret);
            }
            else if (!string.IsNullOrWhiteSpace(hostDefaults.WebhookSecretHeaderName))
            {
                request.Headers.TryAddWithoutValidation(
                    hostDefaults.WebhookSecretHeaderName.Trim(),
                    hostDefaults.WebhookSecretHeaderValue ?? "");
            }

            var json = JsonSerializer.Serialize(
                payload,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                });
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Activity webhook returned {StatusCode} for Type={Type}",
                    (int)response.StatusCode,
                    evt.Type);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Activity webhook failed for Type={Type}", evt.Type);
        }
    }

    private static object BuildPayload(string format, string text, ActivityEvent evt)
    {
        var normalized = format.Trim();
        if (normalized.Equals("Slack", StringComparison.OrdinalIgnoreCase))
            return new { text };

        if (normalized.Equals("Discord", StringComparison.OrdinalIgnoreCase))
            return new { content = text };

        return new
        {
            source = "regkasse-activity",
            type = evt.Type.ToString(),
            severity = evt.Severity,
            title = evt.Title,
            description = evt.Description,
            createdAtUtc = evt.CreatedAtUtc,
            tenantId = evt.TenantId,
            entityId = evt.EntityId,
            entityType = evt.EntityType,
        };
    }
}
