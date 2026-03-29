using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// İsteğe bağlı HTTP webhook; kapalı veya URL yoksa no-op. Hata durumunda yalnızca log (iş akışı kesilmez).
/// </summary>
public sealed class WebhookBackupAlertPublisher : IBackupAlertPublisher
{
    public const string HttpClientName = "OperationalDrAlertWebhook";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<OperationalDrAlertOptions> _options;
    private readonly ILogger<WebhookBackupAlertPublisher> _logger;

    public WebhookBackupAlertPublisher(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<OperationalDrAlertOptions> options,
        ILogger<WebhookBackupAlertPublisher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public void Publish(BackupAlertEvent evt)
    {
        var o = _options.CurrentValue;
        if (!o.WebhookEnabled || string.IsNullOrWhiteSpace(o.WebhookUrl))
            return;

        var timeoutSec = Math.Clamp(o.WebhookTimeoutSeconds, 1, 120);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, o.WebhookUrl);
            if (!string.IsNullOrWhiteSpace(o.WebhookSecretHeaderName))
            {
                request.Headers.TryAddWithoutValidation(
                    o.WebhookSecretHeaderName.Trim(),
                    o.WebhookSecretHeaderValue ?? "");
            }

            var payload = new Dictionary<string, object?>
            {
                ["kind"] = evt.Kind.ToString(),
                ["backupRunId"] = evt.BackupRunId,
                ["restoreVerificationRunId"] = evt.RestoreVerificationRunId,
                ["correlationId"] = evt.CorrelationId,
                ["message"] = evt.Message
            };
            if (evt.Data is { Count: > 0 })
                payload["data"] = new Dictionary<string, string>(evt.Data);

            var json = JsonSerializer.Serialize(
                payload,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = client.SendAsync(request, cts.Token).ConfigureAwait(false).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Operational DR webhook alert returned {StatusCode} for kind={Kind}",
                    (int)response.StatusCode,
                    evt.Kind);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Operational DR webhook alert failed for kind={Kind}", evt.Kind);
        }
    }
}
