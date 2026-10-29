using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Order;

public interface IWoltOrderNotificationService
{
    Task<OnlineOrderIntegrationCommandResult> AcceptAsync(
        string rawBody,
        string? signatureHeader,
        CancellationToken ct = default);
}

public sealed class WoltOrderNotificationService : IWoltOrderNotificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly IOptions<OnlineOrderIntegrationOptions> _options;
    private readonly IWoltAccessTokenSource _tokens;
    private readonly IWoltOAuthClient _oauth;
    private readonly IOnlineOrderIntegrationWebhookService _webhooks;
    private readonly TimeProvider _time;
    private readonly ILogger<WoltOrderNotificationService> _logger;

    public WoltOrderNotificationService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenant,
        IOptions<OnlineOrderIntegrationOptions> options,
        IWoltAccessTokenSource tokens,
        IWoltOAuthClient oauth,
        IOnlineOrderIntegrationWebhookService webhooks,
        TimeProvider time,
        ILogger<WoltOrderNotificationService> logger)
    {
        _dbFactory = dbFactory;
        _tenant = tenant;
        _options = options;
        _tokens = tokens;
        _oauth = oauth;
        _webhooks = webhooks;
        _time = time;
        _logger = logger;
    }

    public async Task<OnlineOrderIntegrationCommandResult> AcceptAsync(
        string rawBody,
        string? signatureHeader,
        CancellationToken ct = default)
    {
        var secret = _options.Value.Wolt.ClientSecret;
        if (!OnlineOrderIntegrationSignature.IsValid(rawBody ?? string.Empty, secret, signatureHeader))
            return Fail(StatusCodes.Status400BadRequest, "SIGNATURE_INVALID", "Signature is missing or invalid.");

        WoltWebhookNotification? notification;
        try
        {
            notification = JsonSerializer.Deserialize<WoltWebhookNotification>(rawBody ?? string.Empty, JsonOptions);
        }
        catch (JsonException)
        {
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Payload could not be read.");
        }

        if (notification is null
            || !string.Equals(notification.Type, "order.notification", StringComparison.OrdinalIgnoreCase))
            return OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK);

        var status = notification.Order?.Status?.Trim() ?? string.Empty;
        var created = status.Equals("CREATED", StringComparison.OrdinalIgnoreCase);
        var production = status.Equals("PRODUCTION", StringComparison.OrdinalIgnoreCase);
        if (!created && !production)
            return OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK);

        var orderId = notification.Order?.Id?.Trim() ?? string.Empty;
        var venueId = notification.Order?.VenueId?.Trim() ?? string.Empty;
        if (orderId.Length is < 1 or > 128 || venueId.Length is < 1 or > 128)
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "External order id and location id are required.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var location = await db.OnlineOrderIntegrationLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.Provider == OnlineOrderIntegrationSignatureStatus.WoltProvider
                     && l.ExternalLocationId == venueId
                     && l.IsActive,
                ct);
        if (location is null || !location.Enabled)
        {
            _logger.LogInformation("Wolt webhook rejected for venue {VenueId}: unknown or disabled.", venueId);
            return Fail(StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", "Location was not found.");
        }

        var existingId = await db.OnlineOrderIntegrations
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == location.TenantId
                        && r.Provider == OnlineOrderIntegrationSignatureStatus.WoltProvider
                        && r.ExternalOrderId == orderId
                        && r.IsActive)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);
        if (existingId is Guid already)
            return OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK, already);

        var access = await _tokens.GetAccessTokenAsync(location.TenantId, venueId, ct);
        if (string.IsNullOrWhiteSpace(access))
            return Fail(StatusCodes.Status502BadGateway, "WOLT_TOKEN_UNAVAILABLE", "Wolt access token is not available.");

        var fetched = await _oauth.GetOrderAsync(access, orderId, ct);
        if (!fetched.Succeeded || string.IsNullOrWhiteSpace(fetched.Body))
            return Fail(StatusCodes.Status502BadGateway, "WOLT_ORDER_FETCH_FAILED", "Wolt order could not be fetched.");

        var mapped = WoltOrderMapper.Map(fetched.Body);
        if (!mapped.Parsed || string.IsNullOrWhiteSpace(mapped.NormalizedJson))
            return Fail(StatusCodes.Status502BadGateway, "WOLT_ORDER_FETCH_FAILED", "Wolt order payload could not be read.");

        OnlineOrderIntegrationWebhookBody? normalized;
        try
        {
            normalized = JsonSerializer.Deserialize<OnlineOrderIntegrationWebhookBody>(mapped.NormalizedJson, JsonOptions);
        }
        catch (JsonException)
        {
            return Fail(StatusCodes.Status502BadGateway, "WOLT_ORDER_FETCH_FAILED", "Wolt order payload could not be read.");
        }

        if (normalized is null
            || !string.Equals(normalized.ExternalOrderId, orderId, StringComparison.Ordinal)
            || !string.Equals(normalized.ExternalLocationId, venueId, StringComparison.Ordinal))
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Wolt order did not match the notification.");

        if (!mapped.CurrencyAccepted)
            return await StoreFailedAsync(location, orderId, venueId, mapped.NormalizedJson, mapped.Error ?? WoltOrderMapper.CurrencyError, ct);

        return await _webhooks.PersistVerifiedNormalizedAsync(
            OnlineOrderIntegrationSignatureStatus.WoltProvider,
            mapped.NormalizedJson,
            ct);
    }

    private async Task<OnlineOrderIntegrationCommandResult> StoreFailedAsync(
        OnlineOrderIntegrationLocation location,
        string orderId,
        string venueId,
        string normalizedJson,
        string error,
        CancellationToken ct)
    {
        _tenant.TenantId = location.TenantId;
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var now = _time.GetUtcNow().UtcDateTime;
        var row = new OnlineOrderIntegration
        {
            Id = Guid.NewGuid(),
            TenantId = location.TenantId,
            Provider = OnlineOrderIntegrationSignatureStatus.WoltProvider,
            ExternalOrderId = orderId,
            ExternalLocationId = venueId,
            Status = OnlineOrderIntegrationStatuses.Failed,
            RawPayload = normalizedJson,
            PayloadSha256 = OnlineOrderIntegrationProcessor.PayloadSha256(normalizedJson),
            LastError = error.Length <= 500 ? error : error[..500],
            ReceivedAt = now,
            ProcessedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            AttemptCount = 1,
            IsActive = true,
        };
        db.OnlineOrderIntegrations.Add(row);
        await db.SaveChangesAsync(ct);
        return OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK, row.Id);
    }

    private static OnlineOrderIntegrationCommandResult Fail(int statusCode, string code, string error) =>
        OnlineOrderIntegrationCommandResult.Fail(statusCode, code, error);

    private sealed class WoltWebhookNotification
    {
        public string? Id { get; set; }

        public string? Type { get; set; }

        public WoltWebhookOrder? Order { get; set; }
    }

    private sealed class WoltWebhookOrder
    {
        public string? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("venue_id")]
        public string? VenueId { get; set; }

        public string? Status { get; set; }
    }
}
