using System.Security.Cryptography;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Order;

public interface IWoltIntegrationConnectService
{
    Task<WoltConnectStartResult> BeginAsync(string? userId, CancellationToken ct = default);

    Task<OnlineOrderIntegrationCommandResult> CompleteAsync(
        string? userId,
        string? code,
        string? state,
        CancellationToken ct = default);
}

public sealed class WoltConnectStartResult
{
    public bool Succeeded { get; init; }

    public int StatusCode { get; init; }

    public string? Code { get; init; }

    public string? Error { get; init; }

    public string? AuthorizationUrl { get; init; }

    public static WoltConnectStartResult Ok(string authorizationUrl) =>
        new() { Succeeded = true, StatusCode = StatusCodes.Status200OK, AuthorizationUrl = authorizationUrl };

    public static WoltConnectStartResult Fail(int statusCode, string code, string error) =>
        new() { Succeeded = false, StatusCode = statusCode, Code = code, Error = error };
}

public sealed class WoltIntegrationConnectService : IWoltIntegrationConnectService
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly IOptions<OnlineOrderIntegrationOptions> _options;
    private readonly IMemoryCache _cache;
    private readonly IWoltOAuthClient _oauth;
    private readonly IOnlineOrderIntegrationProviderService _providers;
    private readonly OnlineOrderIntegrationTokenProtector _protector;
    private readonly TimeProvider _time;
    private readonly ILogger<WoltIntegrationConnectService> _logger;

    public WoltIntegrationConnectService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenant,
        IOptions<OnlineOrderIntegrationOptions> options,
        IMemoryCache cache,
        IWoltOAuthClient oauth,
        IOnlineOrderIntegrationProviderService providers,
        OnlineOrderIntegrationTokenProtector protector,
        TimeProvider time,
        ILogger<WoltIntegrationConnectService> logger)
    {
        _dbFactory = dbFactory;
        _tenant = tenant;
        _options = options;
        _cache = cache;
        _oauth = oauth;
        _providers = providers;
        _protector = protector;
        _time = time;
        _logger = logger;
    }

    public async Task<WoltConnectStartResult> BeginAsync(string? userId, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return WoltConnectStartResult.Fail(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND", "Tenant was not found.");
        if (string.IsNullOrWhiteSpace(userId))
            return WoltConnectStartResult.Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "User is required.");

        var partner = await _providers.RequireEnabledCredentialsAsync(OnlineOrderIntegrationSignatureStatus.WoltProvider, ct);
        if (!partner.Result.Succeeded || partner.Credentials is null)
            return WoltConnectStartResult.Fail(partner.Result.StatusCode, partner.Result.Code ?? OnlineOrderIntegrationProviderStatuses.DisabledCode, partner.Result.Error ?? "Provider is disabled.");

        var wolt = _options.Value.Wolt;
        if (string.IsNullOrWhiteSpace(wolt.RedirectUri)
            || string.IsNullOrWhiteSpace(wolt.AuthorizationEndpoint)
            || !wolt.AuthorizationEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return WoltConnectStartResult.Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Wolt OAuth is not configured.");

        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _cache.Set(
            CacheKey(state),
            new WoltOAuthState(userId.Trim(), tenantId),
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = StateLifetime });

        var url = QueryHelpers.AddQueryString(wolt.AuthorizationEndpoint, new Dictionary<string, string?>
        {
            ["client_id"] = partner.Credentials.ClientId,
            ["redirect_uri"] = wolt.RedirectUri,
            ["state"] = state,
            ["response_type"] = "code",
        });
        _logger.LogInformation("Wolt connect started for tenant {TenantId}.", tenantId);
        return WoltConnectStartResult.Ok(url);
    }

    public async Task<OnlineOrderIntegrationCommandResult> CompleteAsync(
        string? userId,
        string? code,
        string? state,
        CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return Fail(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND", "Tenant was not found.");
        if (string.IsNullOrWhiteSpace(userId))
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "User is required.");
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Code and state are required.");

        if (!_cache.TryGetValue(CacheKey(state), out WoltOAuthState? stored) || stored is null)
            return Fail(StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", "OAuth state was not found.");

        _cache.Remove(CacheKey(state));
        if (!string.Equals(stored.UserId, userId.Trim(), StringComparison.Ordinal) || stored.TenantId != tenantId)
            return Fail(StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", "OAuth state was not found.");

        var partner = await _providers.RequireEnabledCredentialsAsync(OnlineOrderIntegrationSignatureStatus.WoltProvider, ct);
        if (!partner.Result.Succeeded || partner.Credentials is null)
            return Fail(partner.Result.StatusCode, partner.Result.Code ?? OnlineOrderIntegrationProviderStatuses.DisabledCode, partner.Result.Error ?? "Provider is disabled.");

        var exchanged = await _oauth.ExchangeAuthorizationCodeAsync(
            code.Trim(),
            partner.Credentials.ClientId,
            partner.Credentials.ClientSecret,
            ct);
        if (!exchanged.Succeeded
            || exchanged.AccessToken is null
            || exchanged.RefreshToken is null
            || string.IsNullOrWhiteSpace(exchanged.VenueId))
            return Fail(StatusCodes.Status502BadGateway, "WOLT_TOKEN_UNAVAILABLE", "Wolt token exchange failed.");

        var venueId = exchanged.VenueId.Trim();
        if (venueId.Length is < 1 or > 128)
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Wolt venue id is invalid.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var location = await db.OnlineOrderIntegrationLocations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                l => l.Provider == OnlineOrderIntegrationSignatureStatus.WoltProvider
                     && l.ExternalLocationId == venueId,
                ct);
        if (location is not null && location.TenantId != tenantId)
            return Fail(StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", "Location was not found.");

        var now = _time.GetUtcNow().UtcDateTime;
        if (location is null)
        {
            location = new OnlineOrderIntegrationLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Provider = OnlineOrderIntegrationSignatureStatus.WoltProvider,
                ExternalLocationId = venueId,
                Enabled = false,
                CreatedAt = now,
                IsActive = true,
            };
            db.OnlineOrderIntegrationLocations.Add(location);
        }

        location.UpdatedAt = now;
        location.IsActive = true;

        var token = await db.OnlineOrderIntegrationOAuthTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                t => t.TenantId == tenantId
                     && t.Provider == OnlineOrderIntegrationSignatureStatus.WoltProvider
                     && t.ExternalLocationId == venueId,
                ct);
        if (token is null)
        {
            token = new OnlineOrderIntegrationOAuthToken
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Provider = OnlineOrderIntegrationSignatureStatus.WoltProvider,
                ExternalLocationId = venueId,
                CreatedAt = now,
                IsActive = true,
            };
            db.OnlineOrderIntegrationOAuthTokens.Add(token);
        }

        token.AccessToken = _protector.Protect(exchanged.AccessToken);
        token.RefreshToken = _protector.Protect(exchanged.RefreshToken);
        token.AccessTokenExpiresAt = exchanged.ExpiresAtUtc;
        token.UpdatedAt = now;
        token.IsActive = true;
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Wolt venue {VenueId} connected for tenant {TenantId}.", venueId, tenantId);
        return OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK, location.Id);
    }

    private static string CacheKey(string state) => "wolt_oauth_state_" + state;

    private static OnlineOrderIntegrationCommandResult Fail(int statusCode, string code, string error) =>
        OnlineOrderIntegrationCommandResult.Fail(statusCode, code, error);

    private sealed record WoltOAuthState(string UserId, Guid TenantId);
}
