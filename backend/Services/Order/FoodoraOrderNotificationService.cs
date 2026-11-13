using System.Text;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

public interface IFoodoraOrderNotificationService
{
    /// <summary>
    /// Verifies the Authorization header before reading <paramref name="body"/>.
    /// </summary>
    Task<OnlineOrderIntegrationCommandResult> AcceptAsync(
        string? authorizationHeader,
        Stream body,
        CancellationToken ct = default);
}

public sealed class FoodoraOrderNotificationService : IFoodoraOrderNotificationService
{
    private const int OrderFetchAttempts = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly OnlineOrderIntegrationTokenProtector _protector;
    private readonly IFoodoraAuthClient _auth;
    private readonly IFoodoraAccessTokenSource _tokens;
    private readonly IOnlineOrderIntegrationWebhookService _webhooks;
    private readonly OnlineOrderStatusService _status;
    private readonly IVerticalProfileGuard _profiles;
    private readonly TimeProvider _time;
    private readonly ILogger<FoodoraOrderNotificationService> _logger;

    public FoodoraOrderNotificationService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenant,
        OnlineOrderIntegrationTokenProtector protector,
        IFoodoraAuthClient auth,
        IFoodoraAccessTokenSource tokens,
        IOnlineOrderIntegrationWebhookService webhooks,
        OnlineOrderStatusService status,
        IVerticalProfileGuard profiles,
        TimeProvider time,
        ILogger<FoodoraOrderNotificationService> logger)
    {
        _dbFactory = dbFactory;
        _tenant = tenant;
        _protector = protector;
        _auth = auth;
        _tokens = tokens;
        _webhooks = webhooks;
        _status = status;
        _profiles = profiles;
        _time = time;
        _logger = logger;
    }

    public async Task<OnlineOrderIntegrationCommandResult> AcceptAsync(
        string? authorizationHeader,
        Stream body,
        CancellationToken ct = default)
    {
        var credential = await MatchCredentialAsync(authorizationHeader, ct);
        if (credential is null)
            return Fail(StatusCodes.Status400BadRequest, "SIGNATURE_INVALID", "Signature is missing or invalid.");

        string raw;
        using (var reader = new StreamReader(body, Encoding.UTF8))
            raw = await reader.ReadToEndAsync(ct);

        var mapped = FoodoraOrderMapper.Map(raw);
        if (!mapped.Parsed || string.IsNullOrWhiteSpace(mapped.OrderId) || string.IsNullOrWhiteSpace(mapped.LocationId))
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Payload could not be read.");

        if (mapped.OrderId.Length > 128 || mapped.LocationId.Length > 128)
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "External order id and location id are required.");

        if (!string.Equals(mapped.ChainId, credential.ChainId, StringComparison.Ordinal))
            return Fail(StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", "Location was not found.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var location = await db.OnlineOrderIntegrationLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.Provider == OnlineOrderIntegrationSignatureStatus.FoodoraProvider
                     && l.ExternalLocationId == mapped.LocationId
                     && l.IsActive,
                ct);
        if (location is null || !location.Enabled || location.TenantId != credential.TenantId)
        {
            _logger.LogInformation("Foodora webhook rejected for vendor {VendorId}: unknown or disabled.", mapped.LocationId);
            return Fail(StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", "Location was not found.");
        }

        _tenant.TenantId = location.TenantId;
        if (!await _profiles.IsFeatureEnabledAsync(VerticalProfileGuard.MarketplaceOrdersFeature, ct))
            return Fail(StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", "Location was not found.");

        var existing = await db.OnlineOrderIntegrations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                r => r.TenantId == location.TenantId
                     && r.Provider == OnlineOrderIntegrationSignatureStatus.FoodoraProvider
                     && r.ExternalOrderId == mapped.OrderId
                     && r.IsActive,
                ct);
        if (existing is not null)
            return await UpdateExistingAsync(db, existing, mapped, ct);

        if (!mapped.CountryAccepted)
        {
            return await StoreFailedAsync(
                location,
                mapped.OrderId,
                mapped.LocationId,
                mapped.NormalizedJson ?? raw,
                mapped.Error ?? FoodoraOrderMapper.CountryError,
                mapped.TransportType,
                ct);
        }

        if (!mapped.ItemsPresent)
        {
            var originalOrderId = mapped.OrderId;
            var originalLocationId = mapped.LocationId;
            var fetched = await FetchOrderWithItemsAsync(credential.TenantId, mapped.ChainId, originalOrderId, ct);
            if (!fetched.Succeeded || string.IsNullOrWhiteSpace(fetched.Body))
                return Fail(StatusCodes.Status502BadGateway, "FOODORA_ORDER_FETCH_FAILED", "Foodora order could not be fetched.");

            mapped = FoodoraOrderMapper.Map(fetched.Body);
            if (!mapped.Parsed
                || !string.Equals(mapped.OrderId, originalOrderId, StringComparison.Ordinal)
                || !mapped.ItemsPresent
                || !string.Equals(mapped.LocationId, originalLocationId, StringComparison.Ordinal))
                return Fail(StatusCodes.Status502BadGateway, "FOODORA_ORDER_FETCH_FAILED", "Foodora order could not be fetched.");

            if (!string.Equals(mapped.ChainId, credential.ChainId, StringComparison.Ordinal))
                return Fail(StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", "Location was not found.");

            if (!mapped.CountryAccepted)
            {
                return await StoreFailedAsync(
                    location,
                    mapped.OrderId!,
                    mapped.LocationId!,
                    mapped.NormalizedJson ?? fetched.Body,
                    mapped.Error ?? FoodoraOrderMapper.CountryError,
                    mapped.TransportType,
                    ct);
            }
        }

        if (string.IsNullOrWhiteSpace(mapped.NormalizedJson))
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Payload could not be read.");

        OnlineOrderIntegrationWebhookBody? normalized;
        try
        {
            normalized = JsonSerializer.Deserialize<OnlineOrderIntegrationWebhookBody>(mapped.NormalizedJson, JsonOptions);
        }
        catch (JsonException)
        {
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Payload could not be read.");
        }

        if (normalized is null || !string.Equals(normalized.ExternalOrderId, mapped.OrderId, StringComparison.Ordinal))
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Payload could not be read.");

        var persisted = await _webhooks.PersistVerifiedNormalizedAsync(
            OnlineOrderIntegrationSignatureStatus.FoodoraProvider,
            mapped.NormalizedJson,
            ct);
        if (persisted.Succeeded && persisted.Id is Guid persistedId)
            await RememberOutcomeAsync(persistedId, mapped, ct);
        return persisted;
    }

    private async Task<OnlineOrderIntegrationTenantCredential?> MatchCredentialAsync(
        string? authorizationHeader,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var rows = await db.OnlineOrderIntegrationTenantCredentials
            .IgnoreQueryFilters()
            .Where(c => c.Provider == OnlineOrderIntegrationSignatureStatus.FoodoraProvider && c.IsActive)
            .ToListAsync(ct);

        OnlineOrderIntegrationTenantCredential? match = null;
        var matches = 0;
        foreach (var row in rows)
        {
            string token;
            try
            {
                token = _protector.Unprotect(row.WebhookToken);
            }
            catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
            {
                continue;
            }

            if (!FoodoraBasicAuth.Matches(authorizationHeader, token))
                continue;

            matches++;
            match = row;
        }

        return matches == 1 ? match : null;
    }

    private async Task<OnlineOrderIntegrationCommandResult> UpdateExistingAsync(
        AppDbContext db,
        OnlineOrderIntegration existing,
        FoodoraOrderMapResult mapped,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(mapped.TransportType))
            existing.FoodoraTransportType = Trim(mapped.TransportType, 32);
        existing.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        if (existing.OnlineOrderId is Guid orderId && !string.IsNullOrWhiteSpace(mapped.KitchenStatus))
            await _status.ApplyFoodoraInboundAsync(orderId, mapped.KitchenStatus, ct);

        return OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK, existing.Id);
    }

    private async Task RememberOutcomeAsync(Guid integrationId, FoodoraOrderMapResult mapped, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.OnlineOrderIntegrations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == integrationId, ct);
        if (row is null)
            return;

        if (!string.IsNullOrWhiteSpace(mapped.TransportType))
            row.FoodoraTransportType = Trim(mapped.TransportType, 32);
        row.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        if (row.OnlineOrderId is Guid orderId
            && !string.IsNullOrWhiteSpace(mapped.KitchenStatus)
            && !string.Equals(mapped.KitchenStatus, OnlineOrderStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            await _status.ApplyFoodoraInboundAsync(orderId, mapped.KitchenStatus, ct);
    }

    private async Task<FoodoraHttpResult> FetchOrderWithItemsAsync(
        Guid tenantId,
        string? chainId,
        string orderId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(chainId))
            return FoodoraHttpResult.Fail(StatusCodes.Status502BadGateway);

        var access = await _tokens.GetAccessAsync(tenantId, ct);
        if (access is null)
            return FoodoraHttpResult.Fail(StatusCodes.Status502BadGateway);

        FoodoraHttpResult last = FoodoraHttpResult.Fail(StatusCodes.Status502BadGateway);
        for (var attempt = 0; attempt < OrderFetchAttempts; attempt++)
        {
            last = await _auth.GetOrderAsync(access.AccessToken, access.ChainId, orderId, ct);
            if (last.Succeeded && !string.IsNullOrWhiteSpace(last.Body))
            {
                var mapped = FoodoraOrderMapper.Map(last.Body);
                if (mapped.ItemsPresent)
                    return last;
            }
        }

        return last.Succeeded
            ? FoodoraHttpResult.Fail(StatusCodes.Status502BadGateway)
            : last;
    }

    private async Task<OnlineOrderIntegrationCommandResult> StoreFailedAsync(
        OnlineOrderIntegrationLocation location,
        string orderId,
        string venueId,
        string normalizedJson,
        string error,
        string? transportType,
        CancellationToken ct)
    {
        _tenant.TenantId = location.TenantId;
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var now = _time.GetUtcNow().UtcDateTime;
        var row = new OnlineOrderIntegration
        {
            Id = Guid.NewGuid(),
            TenantId = location.TenantId,
            Provider = OnlineOrderIntegrationSignatureStatus.FoodoraProvider,
            ExternalOrderId = orderId,
            ExternalLocationId = venueId,
            ExternalChannel = "foodora",
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
            FoodoraTransportType = Trim(transportType, 32),
        };
        db.OnlineOrderIntegrations.Add(row);
        await db.SaveChangesAsync(ct);
        return OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK, row.Id);
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static OnlineOrderIntegrationCommandResult Fail(int statusCode, string code, string error) =>
        OnlineOrderIntegrationCommandResult.Fail(statusCode, code, error);
}
