using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Registers and delivers tenant TSE webhooks via named HttpClient.
/// Secret sent as <c>X-Tse-Webhook-Secret</c> (same pattern as activity webhooks — not HMAC).
/// </summary>
public sealed class TseWebhookService : ITseWebhookService
{
    public const string HttpClientName = "TseWebhook";
    public const string SecretHeaderName = "X-Tse-Webhook-Secret";

    private const int MaxDeliveriesTake = 200;
    private const int FailoverToFailingAfter = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TseWebhookService> _logger;

    public TseWebhookService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<TseWebhookService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<TseWebhookRegistrationDto> RegisterWebhookAsync(
        RegisterTseWebhookRequestDto request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(request));

        await RequireTenantAsync(request.TenantId, cancellationToken).ConfigureAwait(false);

        if (!Uri.TryCreate(request.Url?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("Url must be an absolute http(s) URL.");
        }

        // Prefer HTTPS outside localhost
        if (uri.Scheme == Uri.UriSchemeHttp
            && !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            && uri.Host != "127.0.0.1")
        {
            throw new ArgumentException("Non-local webhook URLs must use HTTPS.");
        }

        var events = (request.Events ?? new List<string>())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Where(TseWebhookEventTypes.IsKnown)
            .Select(TseWebhookEventTypes.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (events.Count == 0)
            throw new ArgumentException(
                $"At least one known event is required. Allowed: {string.Join(", ", TseWebhookEventTypes.All)}");

        var row = new TseWebhookRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Url = uri.ToString(),
            EventsCsv = string.Join(',', events),
            Secret = string.IsNullOrWhiteSpace(request.Secret) ? null : Truncate(request.Secret.Trim(), 256),
            Status = TseWebhookStatuses.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Truncate(actorUserId, 450),
        };

        _db.TseWebhooks.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapRegistration(row);
    }

    public async Task<IReadOnlyList<TseWebhookRegistrationDto>> ListWebhooksAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var rows = await _db.TseWebhooks.AsNoTracking()
            .Where(w => w.TenantId == tenantId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(MapRegistration).ToList();
    }

    public async Task DeleteWebhookAsync(Guid webhookId, CancellationToken cancellationToken = default)
    {
        if (webhookId == Guid.Empty)
            throw new ArgumentException("webhookId is required.", nameof(webhookId));

        var row = await _db.TseWebhooks
            .FirstOrDefaultAsync(w => w.Id == webhookId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"Webhook {webhookId} was not found.");

        _db.TseWebhooks.Remove(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TseWebhookDeliveryResultDto> TriggerWebhookAsync(
        Guid webhookId,
        TseWebhookEventDto @event,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (webhookId == Guid.Empty)
            throw new ArgumentException("webhookId is required.", nameof(webhookId));

        var webhook = await _db.TseWebhooks
            .FirstOrDefaultAsync(w => w.Id == webhookId, cancellationToken)
            .ConfigureAwait(false);
        if (webhook is null)
            throw new KeyNotFoundException($"Webhook {webhookId} was not found.");

        if (string.Equals(webhook.Status, TseWebhookStatuses.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            return new TseWebhookDeliveryResultDto
            {
                DeliveryId = Guid.Empty,
                WebhookId = webhookId,
                EventId = @event.EventId == Guid.Empty ? Guid.NewGuid() : @event.EventId,
                Success = false,
                Message = "Webhook is disabled.",
                DeliveredAt = DateTime.UtcNow,
            };
        }

        var eventType = string.IsNullOrWhiteSpace(@event.EventType)
            ? TseWebhookEventTypes.Test
            : @event.EventType.Trim();
        if (!TseWebhookEventTypes.IsKnown(eventType))
            throw new ArgumentException($"Unknown event type '{eventType}'.");

        eventType = TseWebhookEventTypes.Normalize(eventType);
        var subscribed = webhook.GetEventList();
        if (!subscribed.Contains(eventType, StringComparer.OrdinalIgnoreCase)
            && !string.Equals(eventType, TseWebhookEventTypes.Test, StringComparison.OrdinalIgnoreCase))
        {
            return new TseWebhookDeliveryResultDto
            {
                DeliveryId = Guid.Empty,
                WebhookId = webhookId,
                EventId = @event.EventId == Guid.Empty ? Guid.NewGuid() : @event.EventId,
                Success = false,
                Message = $"Webhook is not subscribed to '{eventType}'.",
                DeliveredAt = DateTime.UtcNow,
            };
        }

        var eventId = @event.EventId == Guid.Empty ? Guid.NewGuid() : @event.EventId;
        var occurredAt = @event.OccurredAt == default ? DateTime.UtcNow : @event.OccurredAt.ToUniversalTime();
        var payloadObj = new
        {
            eventId,
            eventType,
            occurredAt,
            tenantId = webhook.TenantId,
            webhookId = webhook.Id,
            payload = @event.Payload ?? new { },
        };
        var payloadJson = JsonSerializer.Serialize(payloadObj, JsonOptions);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json"),
        };
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(webhook.Secret))
            requestMessage.Headers.TryAddWithoutValidation(SecretHeaderName, webhook.Secret);

        var deliveredAt = DateTime.UtcNow;
        var success = false;
        int? httpStatus = null;
        string? snippet = null;

        try
        {
            using var response = await client.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
            httpStatus = (int)response.StatusCode;
            success = response.IsSuccessStatusCode;
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            snippet = Truncate(body, 1000);
            if (!success)
            {
                _logger.LogWarning(
                    "TSE webhook delivery failed WebhookId={WebhookId} Status={Status}",
                    webhookId,
                    httpStatus);
            }
        }
        catch (Exception ex)
        {
            snippet = Truncate(ex.Message, 1000);
            _logger.LogWarning(ex, "TSE webhook delivery exception WebhookId={WebhookId}", webhookId);
        }

        var delivery = new TseWebhookDelivery
        {
            Id = Guid.NewGuid(),
            WebhookId = webhook.Id,
            TenantId = webhook.TenantId,
            EventId = eventId,
            EventType = eventType,
            OccurredAt = occurredAt,
            DeliveredAt = deliveredAt,
            Success = success,
            HttpStatus = httpStatus,
            ResponseSnippet = snippet,
            PayloadJson = Truncate(payloadJson, 2000)!,
        };
        _db.TseWebhookDeliveries.Add(delivery);

        webhook.LastDeliveryAt = deliveredAt;
        webhook.LastDeliverySuccess = success;
        webhook.UpdatedAt = deliveredAt;
        if (success)
        {
            webhook.ConsecutiveFailures = 0;
            if (string.Equals(webhook.Status, TseWebhookStatuses.Failing, StringComparison.OrdinalIgnoreCase))
                webhook.Status = TseWebhookStatuses.Active;
        }
        else
        {
            webhook.ConsecutiveFailures += 1;
            if (webhook.ConsecutiveFailures >= FailoverToFailingAfter)
                webhook.Status = TseWebhookStatuses.Failing;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TseWebhookDeliveryResultDto
        {
            DeliveryId = delivery.Id,
            WebhookId = webhook.Id,
            EventId = eventId,
            Success = success,
            HttpStatus = httpStatus,
            Message = success ? "Delivered." : (snippet ?? "Delivery failed."),
            DeliveredAt = deliveredAt,
        };
    }

    public async Task<IReadOnlyList<TseWebhookDeliveryLogDto>> GetWebhookEventsAsync(
        Guid webhookId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (webhookId == Guid.Empty)
            throw new ArgumentException("webhookId is required.", nameof(webhookId));

        var exists = await _db.TseWebhooks.AsNoTracking()
            .AnyAsync(w => w.Id == webhookId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new KeyNotFoundException($"Webhook {webhookId} was not found.");

        take = Math.Clamp(take, 1, MaxDeliveriesTake);
        var rows = await _db.TseWebhookDeliveries.AsNoTracking()
            .Where(d => d.WebhookId == webhookId)
            .OrderByDescending(d => d.DeliveredAt)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(d => new TseWebhookDeliveryLogDto
        {
            Id = d.Id,
            EventId = d.EventId,
            EventType = d.EventType,
            OccurredAt = d.OccurredAt,
            DeliveredAt = d.DeliveredAt,
            Success = d.Success,
            HttpStatus = d.HttpStatus,
            ResponseSnippet = d.ResponseSnippet,
            PayloadJson = d.PayloadJson,
        }).ToList();
    }

    public Task<TseWebhookDeliveryResultDto> TestWebhookAsync(
        Guid webhookId,
        CancellationToken cancellationToken = default) =>
        TriggerWebhookAsync(
            webhookId,
            new TseWebhookEventDto
            {
                EventId = Guid.NewGuid(),
                EventType = TseWebhookEventTypes.Test,
                OccurredAt = DateTime.UtcNow,
                Payload = new { message = "TSE webhook test ping", source = "admin" },
            },
            cancellationToken);

    private async Task RequireTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var exists = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
    }

    private static TseWebhookRegistrationDto MapRegistration(TseWebhookRegistration row) => new()
    {
        Id = row.Id,
        TenantId = row.TenantId,
        Url = row.Url,
        Events = row.GetEventList(),
        Status = row.Status,
        CreatedAt = row.CreatedAt,
        LastDeliveryAt = row.LastDeliveryAt,
        LastDeliverySuccess = row.LastDeliverySuccess,
        ConsecutiveFailures = row.ConsecutiveFailures,
        HasSecret = !string.IsNullOrEmpty(row.Secret),
    };

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
