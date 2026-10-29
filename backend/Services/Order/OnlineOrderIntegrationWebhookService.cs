using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Order;

public interface IOnlineOrderIntegrationWebhookService
{
    Task<OnlineOrderIntegrationCommandResult> AcceptAsync(
        string provider,
        string rawBody,
        string? signatureHeader,
        CancellationToken ct = default);

    /// <summary>
    /// Stores a body whose signature was already checked by the caller.
    /// Deliverect still enters through <see cref="AcceptAsync"/>.
    /// </summary>
    Task<OnlineOrderIntegrationCommandResult> PersistVerifiedNormalizedAsync(
        string provider,
        string normalizedBody,
        CancellationToken ct = default);
}

public sealed class OnlineOrderIntegrationWebhookService : IOnlineOrderIntegrationWebhookService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly IOptions<OnlineOrderIntegrationOptions> _options;
    private readonly IOnlineOrderIntegrationProcessor _processor;
    private readonly TimeProvider _time;
    private readonly ILogger<OnlineOrderIntegrationWebhookService> _logger;

    public OnlineOrderIntegrationWebhookService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenant,
        IOptions<OnlineOrderIntegrationOptions> options,
        IOnlineOrderIntegrationProcessor processor,
        TimeProvider time,
        ILogger<OnlineOrderIntegrationWebhookService> logger)
    {
        _dbFactory = dbFactory;
        _tenant = tenant;
        _options = options;
        _processor = processor;
        _time = time;
        _logger = logger;
    }

    public async Task<OnlineOrderIntegrationCommandResult> AcceptAsync(
        string provider,
        string rawBody,
        string? signatureHeader,
        CancellationToken ct = default)
    {
        var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedProvider.Length is < 1 or > 32)
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Provider is invalid.");

        OnlineOrderIntegrationWebhookBody? body;
        try
        {
            body = JsonSerializer.Deserialize<OnlineOrderIntegrationWebhookBody>(rawBody ?? string.Empty, JsonOptions);
        }
        catch (JsonException)
        {
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Payload could not be read.");
        }

        var externalOrderId = body?.ExternalOrderId?.Trim() ?? string.Empty;
        var externalLocationId = body?.ExternalLocationId?.Trim() ?? string.Empty;
        if (body is null || externalOrderId.Length is < 1 or > 128 || externalLocationId.Length is < 1 or > 128)
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "External order id and location id are required.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var location = await db.OnlineOrderIntegrationLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.Provider == normalizedProvider
                     && l.ExternalLocationId == externalLocationId
                     && l.IsActive,
                ct);
        if (location is null || !location.Enabled)
        {
            _logger.LogInformation(
                "Online-order webhook rejected for provider {Provider} location {LocationId}: unknown or disabled.",
                normalizedProvider,
                externalLocationId);
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", "Location was not found.");
        }

        var secretKey = normalizedProvider + ":" + externalLocationId;
        _options.Value.WebhookSecrets.TryGetValue(secretKey, out var secret);
        if (!OnlineOrderIntegrationSignature.IsValid(rawBody ?? string.Empty, secret, signatureHeader))
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status400BadRequest, "SIGNATURE_INVALID", "Signature is missing or invalid.");

        return await InsertAndProcessAsync(db, location, normalizedProvider, rawBody ?? string.Empty, body, externalOrderId, externalLocationId, ct);
    }

    public async Task<OnlineOrderIntegrationCommandResult> PersistVerifiedNormalizedAsync(
        string provider,
        string normalizedBody,
        CancellationToken ct = default)
    {
        var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedProvider.Length is < 1 or > 32)
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Provider is invalid.");

        OnlineOrderIntegrationWebhookBody? body;
        try
        {
            body = JsonSerializer.Deserialize<OnlineOrderIntegrationWebhookBody>(normalizedBody ?? string.Empty, JsonOptions);
        }
        catch (JsonException)
        {
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Payload could not be read.");
        }

        var externalOrderId = body?.ExternalOrderId?.Trim() ?? string.Empty;
        var externalLocationId = body?.ExternalLocationId?.Trim() ?? string.Empty;
        if (body is null || externalOrderId.Length is < 1 or > 128 || externalLocationId.Length is < 1 or > 128)
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "External order id and location id are required.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var location = await db.OnlineOrderIntegrationLocations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                l => l.Provider == normalizedProvider
                     && l.ExternalLocationId == externalLocationId
                     && l.IsActive,
                ct);
        if (location is null || !location.Enabled)
        {
            _logger.LogInformation(
                "Online-order webhook rejected for provider {Provider} location {LocationId}: unknown or disabled.",
                normalizedProvider,
                externalLocationId);
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", "Location was not found.");
        }

        return await InsertAndProcessAsync(db, location, normalizedProvider, normalizedBody ?? string.Empty, body, externalOrderId, externalLocationId, ct);
    }

    private async Task<OnlineOrderIntegrationCommandResult> InsertAndProcessAsync(
        AppDbContext db,
        OnlineOrderIntegrationLocation location,
        string normalizedProvider,
        string rawBody,
        OnlineOrderIntegrationWebhookBody body,
        string externalOrderId,
        string externalLocationId,
        CancellationToken ct)
    {
        _tenant.TenantId = location.TenantId;

        var existingId = await db.OnlineOrderIntegrations
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == location.TenantId
                        && r.Provider == normalizedProvider
                        && r.ExternalOrderId == externalOrderId
                        && r.IsActive)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);
        if (existingId is Guid already)
            return OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK, already);

        var now = _time.GetUtcNow().UtcDateTime;
        var row = new OnlineOrderIntegration
        {
            Id = Guid.NewGuid(),
            TenantId = location.TenantId,
            Provider = normalizedProvider,
            ExternalOrderId = externalOrderId,
            ExternalChannel = TrimTo(body.ExternalChannel, 64),
            ExternalLocationId = externalLocationId,
            Status = OnlineOrderIntegrationStatuses.Received,
            RawPayload = rawBody,
            PayloadSha256 = OnlineOrderIntegrationProcessor.PayloadSha256(rawBody),
            ReceivedAt = now,
            CreatedAt = now,
            AttemptCount = 0,
            IsActive = true,
        };

        db.OnlineOrderIntegrations.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var raced = await db.OnlineOrderIntegrations
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == location.TenantId
                            && r.Provider == normalizedProvider
                            && r.ExternalOrderId == externalOrderId)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync(ct);
            return OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK, raced);
        }

        await _processor.ProcessAsync(row.Id, ct);
        return OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK, row.Id);
    }

    private static string? TrimTo(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
