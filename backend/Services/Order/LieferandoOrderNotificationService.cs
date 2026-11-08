using System.Text;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

public interface ILieferandoOrderNotificationService
{
    /// <summary>
    /// Verifies <paramref name="authorizationHeader"/> before reading <paramref name="body"/>.
    /// </summary>
    Task<OnlineOrderIntegrationCommandResult> AcceptAsync(
        string? authorizationHeader,
        string? hashHeader,
        Stream body,
        CancellationToken ct = default);
}

public sealed class LieferandoOrderNotificationService : ILieferandoOrderNotificationService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly OnlineOrderIntegrationTokenProtector _protector;
    private readonly IOnlineOrderIntegrationWebhookService _webhooks;
    private readonly IVerticalProfileGuard _profiles;
    private readonly TimeProvider _time;
    private readonly ILogger<LieferandoOrderNotificationService> _logger;

    public LieferandoOrderNotificationService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenant,
        OnlineOrderIntegrationTokenProtector protector,
        IOnlineOrderIntegrationWebhookService webhooks,
        IVerticalProfileGuard profiles,
        TimeProvider time,
        ILogger<LieferandoOrderNotificationService> logger)
    {
        _dbFactory = dbFactory;
        _tenant = tenant;
        _protector = protector;
        _webhooks = webhooks;
        _profiles = profiles;
        _time = time;
        _logger = logger;
    }

    public async Task<OnlineOrderIntegrationCommandResult> AcceptAsync(
        string? authorizationHeader,
        string? hashHeader,
        Stream body,
        CancellationToken ct = default)
    {
        var credential = await MatchCredentialAsync(authorizationHeader, ct);
        if (credential is null)
            return Fail(StatusCodes.Status400BadRequest, "SIGNATURE_INVALID", "Signature is missing or invalid.");

        if (!await ProviderEnabledAsync(ct))
            return Fail(StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", "Location was not found.");

        string raw;
        using (var reader = new StreamReader(body, Encoding.UTF8))
            raw = await reader.ReadToEndAsync(ct);

        if (!string.IsNullOrWhiteSpace(credential.ClientSecret))
        {
            string secret;
            try
            {
                secret = _protector.Unprotect(credential.ClientSecret);
            }
            catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
            {
                return Fail(StatusCodes.Status400BadRequest, "SIGNATURE_INVALID", "Signature is missing or invalid.");
            }

            if (!LieferandoWebhookAuth.HashMatches(raw, secret, hashHeader))
                return Fail(StatusCodes.Status400BadRequest, "SIGNATURE_INVALID", "Signature is missing or invalid.");
        }

        var mapped = LieferandoOrderMapper.Map(raw);
        if (!mapped.Parsed || string.IsNullOrWhiteSpace(mapped.OrderId) || string.IsNullOrWhiteSpace(mapped.LocationId))
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Payload could not be read.");

        if (mapped.OrderId.Length > 128 || mapped.LocationId.Length > 128)
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "External order id and location id are required.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var location = await db.OnlineOrderIntegrationLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.Provider == OnlineOrderIntegrationSignatureStatus.LieferandoProvider
                     && l.ExternalLocationId == mapped.LocationId
                     && l.IsActive,
                ct);
        if (location is null || !location.Enabled || location.TenantId != credential.TenantId)
        {
            _logger.LogInformation("Lieferando webhook rejected for location {LocationId}: unknown or disabled.", mapped.LocationId);
            return Fail(StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", "Location was not found.");
        }

        _tenant.TenantId = location.TenantId;
        if (!await _profiles.IsFeatureEnabledAsync(VerticalProfileGuard.MarketplaceOrdersFeature, ct))
            return Fail(StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", "Location was not found.");

        var existing = await db.OnlineOrderIntegrations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.TenantId == location.TenantId
                     && r.Provider == OnlineOrderIntegrationSignatureStatus.LieferandoProvider
                     && r.ExternalOrderId == mapped.OrderId
                     && r.IsActive,
                ct);
        if (existing is not null)
            return await ReplayAsync(existing, ct);

        if (!string.IsNullOrWhiteSpace(mapped.Error))
            return await StoreRejectedAsync(location, mapped, raw, mapped.Error, ct);

        if (string.IsNullOrWhiteSpace(mapped.NormalizedJson))
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Payload could not be read.");

        var persisted = await _webhooks.PersistVerifiedNormalizedAsync(
            OnlineOrderIntegrationSignatureStatus.LieferandoProvider,
            mapped.NormalizedJson,
            ct);
        if (!persisted.Succeeded || persisted.Id is not Guid persistedId)
            return persisted;

        return await AckAsync(persistedId, ct);
    }

    private async Task<MatchedCredential?> MatchCredentialAsync(
        string? authorizationHeader,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var rows = await db.OnlineOrderIntegrationTenantCredentials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.Provider == OnlineOrderIntegrationSignatureStatus.LieferandoProvider && c.IsActive)
            .Select(c => new { c.TenantId, c.ClientId, c.ClientSecret })
            .ToListAsync(ct);
        var copied = rows.Select(c => new MatchedCredential(c.TenantId, c.ClientId, c.ClientSecret)).ToList();

        MatchedCredential? match = null;
        var matches = 0;
        foreach (var row in copied)
        {
            string apiKey;
            try
            {
                apiKey = _protector.Unprotect(row.ClientId);
            }
            catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
            {
                continue;
            }

            if (!LieferandoWebhookAuth.AuthorizationMatches(authorizationHeader, apiKey))
                continue;

            matches++;
            match = row;
        }

        return matches == 1 ? match : null;
    }

    private sealed record MatchedCredential(Guid TenantId, string ClientId, string ClientSecret);

    private async Task<bool> ProviderEnabledAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.OnlineOrderIntegrationProviders.AsNoTracking()
            .AnyAsync(
                p => p.Provider == OnlineOrderIntegrationSignatureStatus.LieferandoProvider
                     && p.IsActive
                     && p.Enabled,
                ct);
    }

    private async Task<OnlineOrderIntegrationCommandResult> ReplayAsync(
        OnlineOrderIntegration existing,
        CancellationToken ct)
    {
        var replay = await ReadOrderNumberAsync(existing, ct);
        if (replay is not null)
            return replay;

        return OutcomeWithoutOrder(existing);
    }

    private async Task<OnlineOrderIntegrationCommandResult> AckAsync(Guid integrationId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.OnlineOrderIntegrations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == integrationId, ct);
        if (row is null)
            return Fail(StatusCodes.Status422UnprocessableEntity, LieferandoOrderMapper.RejectedCode, "Payload was rejected.");

        var created = await ReadOrderNumberAsync(row, ct);
        return created ?? OutcomeWithoutOrder(row);
    }

    private async Task<OnlineOrderIntegrationCommandResult?> ReadOrderNumberAsync(
        OnlineOrderIntegration row,
        CancellationToken ct)
    {
        if (row.OnlineOrderId is not Guid orderId)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var number = await db.OnlineOrders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(o => o.Id == orderId && o.TenantId == row.TenantId)
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(number)
            ? null
            : OnlineOrderIntegrationCommandResult.WithOrder(StatusCodes.Status200OK, row.Id, number);
    }

    private static OnlineOrderIntegrationCommandResult OutcomeWithoutOrder(OnlineOrderIntegration row)
    {
        if (row.Status == OnlineOrderIntegrationStatuses.Failed)
        {
            return Fail(
                StatusCodes.Status422UnprocessableEntity,
                LieferandoOrderMapper.RejectedCode,
                row.LastError ?? "Payload was rejected.");
        }

        return Fail(
            StatusCodes.Status409Conflict,
            OnlineOrderIntegrationSignatureStatus.UnconfirmedCode,
            "Lieferando signature scheme is unconfirmed.");
    }

    private async Task<OnlineOrderIntegrationCommandResult> StoreRejectedAsync(
        OnlineOrderIntegrationLocation location,
        LieferandoOrderMapResult mapped,
        string raw,
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
            Provider = OnlineOrderIntegrationSignatureStatus.LieferandoProvider,
            ExternalOrderId = mapped.OrderId!,
            ExternalLocationId = mapped.LocationId,
            Status = OnlineOrderIntegrationStatuses.Failed,
            RawPayload = raw,
            PayloadSha256 = OnlineOrderIntegrationProcessor.PayloadSha256(raw),
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
        return Fail(StatusCodes.Status422UnprocessableEntity, LieferandoOrderMapper.RejectedCode, row.LastError!);
    }

    private static OnlineOrderIntegrationCommandResult Fail(int statusCode, string code, string error) =>
        OnlineOrderIntegrationCommandResult.Fail(statusCode, code, error);
}
