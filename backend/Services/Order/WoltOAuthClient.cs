using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Order;

public interface IWoltOAuthClient
{
    Task<WoltTokenResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken ct = default);

    Task<WoltTokenResult> ExchangeAuthorizationCodeAsync(
        string code,
        string clientId,
        string clientSecret,
        CancellationToken ct = default);

    Task<WoltTokenResult> RefreshAsync(string refreshToken, CancellationToken ct = default);

    Task<WoltTokenResult> RefreshAsync(
        string refreshToken,
        string clientId,
        string clientSecret,
        CancellationToken ct = default);

    Task<WoltOrderFetchResult> GetOrderAsync(string accessToken, string orderId, CancellationToken ct = default);
}

public interface IWoltAccessTokenSource
{
    /// <summary>
    /// Returns a usable access token. Refreshes when the stored access token is inside
    /// the skew window and replaces the single-use refresh token.
    /// </summary>
    Task<string?> GetAccessTokenAsync(Guid tenantId, string externalLocationId, CancellationToken ct = default);
}

public sealed class WoltTokenResult
{
    public bool Succeeded { get; init; }

    public int StatusCode { get; init; }

    public string? AccessToken { get; init; }

    public string? RefreshToken { get; init; }

    public DateTime ExpiresAtUtc { get; init; }

    public string? VenueId { get; init; }

    public static WoltTokenResult Fail(int statusCode) => new() { Succeeded = false, StatusCode = statusCode };

    public static WoltTokenResult Ok(string accessToken, string refreshToken, DateTime expiresAtUtc, string venueId) =>
        new()
        {
            Succeeded = true,
            StatusCode = StatusCodes.Status200OK,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = expiresAtUtc,
            VenueId = venueId,
        };
}

public sealed class WoltOrderFetchResult
{
    public bool Succeeded { get; init; }

    public int StatusCode { get; init; }

    public string? Body { get; init; }

    public static WoltOrderFetchResult Fail(int statusCode) => new() { Succeeded = false, StatusCode = statusCode };

    public static WoltOrderFetchResult Ok(string body) =>
        new() { Succeeded = true, StatusCode = StatusCodes.Status200OK, Body = body };
}

public sealed class WoltOAuthClient : IWoltOAuthClient
{
    public const string HttpClientName = "WoltIntegration";

    private readonly HttpClient _http;
    private readonly IOptions<OnlineOrderIntegrationOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<WoltOAuthClient> _logger;

    public WoltOAuthClient(
        IHttpClientFactory httpClientFactory,
        IOptions<OnlineOrderIntegrationOptions> options,
        TimeProvider time,
        ILogger<WoltOAuthClient> logger)
    {
        _http = httpClientFactory.CreateClient(HttpClientName);
        _options = options;
        _time = time;
        _logger = logger;
    }

    public Task<WoltTokenResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken ct = default)
    {
        var wolt = _options.Value.Wolt;
        return ExchangeAuthorizationCodeAsync(code, wolt.ClientId ?? string.Empty, wolt.ClientSecret ?? string.Empty, ct);
    }

    public Task<WoltTokenResult> ExchangeAuthorizationCodeAsync(
        string code,
        string clientId,
        string clientSecret,
        CancellationToken ct = default)
    {
        var wolt = _options.Value.Wolt;
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = wolt.RedirectUri ?? string.Empty,
        };
        return RequestTokenAsync(fields, clientId, clientSecret, ct);
    }

    public Task<WoltTokenResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var wolt = _options.Value.Wolt;
        return RefreshAsync(refreshToken, wolt.ClientId ?? string.Empty, wolt.ClientSecret ?? string.Empty, ct);
    }

    public Task<WoltTokenResult> RefreshAsync(
        string refreshToken,
        string clientId,
        string clientSecret,
        CancellationToken ct = default)
    {
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        };
        return RequestTokenAsync(fields, clientId, clientSecret, ct);
    }

    public async Task<WoltOrderFetchResult> GetOrderAsync(string accessToken, string orderId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(orderId))
            return WoltOrderFetchResult.Fail(StatusCodes.Status502BadGateway);

        var baseUrl = (_options.Value.Wolt.OrderBaseUrl ?? string.Empty).TrimEnd('/');
        if (!baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return WoltOrderFetchResult.Fail(StatusCodes.Status502BadGateway);

        var url = baseUrl + "/v2/orders/" + Uri.EscapeDataString(orderId.Trim());
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Wolt order fetch returned {StatusCode}.", (int)response.StatusCode);
                return WoltOrderFetchResult.Fail((int)response.StatusCode);
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            return WoltOrderFetchResult.Ok(body);
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Wolt order fetch failed.");
            return WoltOrderFetchResult.Fail(StatusCodes.Status503ServiceUnavailable);
        }
    }

    internal static string? ReadVenueId(string accessToken)
    {
        var parts = accessToken.Split('.');
        if (parts.Length < 2)
            return null;

        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("integration", out var integration)
                && integration.TryGetProperty("venue_id", out var venue)
                && venue.ValueKind == JsonValueKind.String)
            {
                var id = venue.GetString();
                return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
            }
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private async Task<WoltTokenResult> RequestTokenAsync(
        Dictionary<string, string> fields,
        string clientId,
        string clientSecret,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return WoltTokenResult.Fail(StatusCodes.Status400BadRequest);

        var tokenUrl = _options.Value.Wolt.TokenUrl ?? string.Empty;
        if (!tokenUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return WoltTokenResult.Fail(StatusCodes.Status502BadGateway);

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + clientSecret));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Content = new FormUrlEncodedContent(fields);

        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Wolt token endpoint returned {StatusCode}.", (int)response.StatusCode);
                return WoltTokenResult.Fail((int)response.StatusCode);
            }

            var text = await response.Content.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var access = ReadString(root, "access_token");
            var refresh = ReadString(root, "refresh_token");
            var expiresIn = root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var seconds)
                ? seconds
                : 0;
            if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh))
                return WoltTokenResult.Fail(StatusCodes.Status502BadGateway);

            var venueId = ReadVenueId(access);
            if (string.IsNullOrWhiteSpace(venueId))
                return WoltTokenResult.Fail(StatusCodes.Status502BadGateway);

            var expiresAt = _time.GetUtcNow().UtcDateTime.AddSeconds(expiresIn);
            return WoltTokenResult.Ok(access, refresh, expiresAt, venueId);
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Wolt token request failed.");
            return WoltTokenResult.Fail(StatusCodes.Status503ServiceUnavailable);
        }
        catch (JsonException)
        {
            _logger.LogWarning("Wolt token response could not be read.");
            return WoltTokenResult.Fail(StatusCodes.Status502BadGateway);
        }
    }

    private static string? ReadString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
    }
}

public sealed class WoltAccessTokenSource : IWoltAccessTokenSource
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly OnlineOrderIntegrationTokenProtector _protector;
    private readonly IWoltOAuthClient _oauth;
    private readonly IOnlineOrderIntegrationProviderService _providers;
    private readonly TimeProvider _time;
    private readonly ILogger<WoltAccessTokenSource> _logger;

    public WoltAccessTokenSource(
        IDbContextFactory<AppDbContext> dbFactory,
        OnlineOrderIntegrationTokenProtector protector,
        IWoltOAuthClient oauth,
        IOnlineOrderIntegrationProviderService providers,
        TimeProvider time,
        ILogger<WoltAccessTokenSource> logger)
    {
        _dbFactory = dbFactory;
        _protector = protector;
        _oauth = oauth;
        _providers = providers;
        _time = time;
        _logger = logger;
    }

    public async Task<string?> GetAccessTokenAsync(Guid tenantId, string externalLocationId, CancellationToken ct = default)
    {
        var gate = Gates.GetOrAdd(tenantId.ToString("N") + ":" + externalLocationId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var row = await FindAsync(db, tenantId, externalLocationId, ct);
            if (row is null)
                return null;

            string access;
            string refresh;
            try
            {
                access = _protector.Unprotect(row.AccessToken);
                refresh = _protector.Unprotect(row.RefreshToken);
            }
            catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
            {
                _logger.LogWarning("Wolt OAuth token could not be decrypted for location {LocationId}.", externalLocationId);
                return null;
            }

            var skew = _time.GetUtcNow().UtcDateTime.AddMinutes(1);
            if (row.AccessTokenExpiresAt > skew)
                return access;

            var refreshed = await RefreshWithProviderPolicyAsync(refresh, ct);
            if (!refreshed.Succeeded || refreshed.AccessToken is null || refreshed.RefreshToken is null)
                return null;

            var now = _time.GetUtcNow().UtcDateTime;
            row.AccessToken = _protector.Protect(refreshed.AccessToken);
            row.RefreshToken = _protector.Protect(refreshed.RefreshToken);
            row.AccessTokenExpiresAt = refreshed.ExpiresAtUtc;
            row.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return refreshed.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<WoltTokenResult> RefreshWithProviderPolicyAsync(string refresh, CancellationToken ct)
    {
        var admin = await _providers.GetAdminAsync(OnlineOrderIntegrationSignatureStatus.WoltProvider, ct);
        var body = admin.Body;
        if (body is null)
            return WoltTokenResult.Fail(StatusCodes.Status409Conflict);

        var missing = body.CredentialStatus == OnlineOrderIntegrationProviderStatuses.Missing;
        if (!body.Enabled && missing)
            return await _oauth.RefreshAsync(refresh, ct);

        if (!body.Enabled)
            return WoltTokenResult.Fail(StatusCodes.Status409Conflict);

        var partner = await _providers.RequireEnabledCredentialsAsync(OnlineOrderIntegrationSignatureStatus.WoltProvider, ct);
        if (!partner.Result.Succeeded || partner.Credentials is null)
            return WoltTokenResult.Fail(StatusCodes.Status409Conflict);

        return await _oauth.RefreshAsync(refresh, partner.Credentials.ClientId, partner.Credentials.ClientSecret, ct);
    }

    private static Task<OnlineOrderIntegrationOAuthToken?> FindAsync(
        AppDbContext db,
        Guid tenantId,
        string externalLocationId,
        CancellationToken ct) =>
        db.OnlineOrderIntegrationOAuthTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                t => t.TenantId == tenantId
                     && t.Provider == OnlineOrderIntegrationSignatureStatus.WoltProvider
                     && t.ExternalLocationId == externalLocationId
                     && t.IsActive,
                ct);
}
