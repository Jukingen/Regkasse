using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Order;

public interface IFoodoraAuthClient
{
    Task<FoodoraTokenResult> RequestClientCredentialsAsync(
        string clientId,
        string clientSecret,
        CancellationToken ct = default);

    Task<FoodoraHttpResult> GetOrderAsync(
        string accessToken,
        string chainId,
        string orderId,
        CancellationToken ct = default);

    Task<FoodoraHttpResult> PutOrderStatusAsync(
        string accessToken,
        string chainId,
        string orderId,
        string status,
        CancellationToken ct = default);
}

public interface IFoodoraAccessTokenSource
{
    Task<FoodoraAccess?> GetAccessAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed class FoodoraAccess
{
    public string AccessToken { get; init; } = string.Empty;

    public string ChainId { get; init; } = string.Empty;
}

public sealed class FoodoraTokenResult
{
    public bool Succeeded { get; init; }

    public int StatusCode { get; init; }

    public string? AccessToken { get; init; }

    public DateTime ExpiresAtUtc { get; init; }

    public static FoodoraTokenResult Fail(int statusCode) => new() { Succeeded = false, StatusCode = statusCode };

    public static FoodoraTokenResult Ok(string accessToken, DateTime expiresAtUtc) =>
        new()
        {
            Succeeded = true,
            StatusCode = StatusCodes.Status200OK,
            AccessToken = accessToken,
            ExpiresAtUtc = expiresAtUtc,
        };
}

public sealed class FoodoraHttpResult
{
    public bool Succeeded { get; init; }

    public int StatusCode { get; init; }

    public string? Body { get; init; }

    public static FoodoraHttpResult Fail(int statusCode) => new() { Succeeded = false, StatusCode = statusCode };

    public static FoodoraHttpResult Ok(string body) =>
        new() { Succeeded = true, StatusCode = StatusCodes.Status200OK, Body = body };
}

public sealed class FoodoraAuthClient : IFoodoraAuthClient
{
    public const string HttpClientName = "FoodoraIntegration";

    private readonly HttpClient _http;
    private readonly IOptions<OnlineOrderIntegrationOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<FoodoraAuthClient> _logger;

    public FoodoraAuthClient(
        IHttpClientFactory httpClientFactory,
        IOptions<OnlineOrderIntegrationOptions> options,
        TimeProvider time,
        ILogger<FoodoraAuthClient> logger)
    {
        _http = httpClientFactory.CreateClient(HttpClientName);
        _options = options;
        _time = time;
        _logger = logger;
    }

    public async Task<FoodoraTokenResult> RequestClientCredentialsAsync(
        string clientId,
        string clientSecret,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return FoodoraTokenResult.Fail(StatusCodes.Status400BadRequest);

        var tokenUrl = _options.Value.Foodora.TokenUrl ?? string.Empty;
        if (!tokenUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return FoodoraTokenResult.Fail(StatusCodes.Status502BadGateway);

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        });

        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Foodora token endpoint returned {StatusCode}.", (int)response.StatusCode);
                return FoodoraTokenResult.Fail((int)response.StatusCode);
            }

            var text = await response.Content.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var access = ReadString(root, "access_token");
            var expiresIn = root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var seconds)
                ? seconds
                : 0;
            if (string.IsNullOrWhiteSpace(access) || expiresIn <= 0)
                return FoodoraTokenResult.Fail(StatusCodes.Status502BadGateway);

            return FoodoraTokenResult.Ok(access, _time.GetUtcNow().UtcDateTime.AddSeconds(expiresIn));
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Foodora token request failed.");
            return FoodoraTokenResult.Fail(StatusCodes.Status503ServiceUnavailable);
        }
        catch (JsonException)
        {
            _logger.LogWarning("Foodora token response could not be read.");
            return FoodoraTokenResult.Fail(StatusCodes.Status502BadGateway);
        }
    }

    public Task<FoodoraHttpResult> GetOrderAsync(
        string accessToken,
        string chainId,
        string orderId,
        CancellationToken ct = default) =>
        SendOrderAsync(HttpMethod.Get, accessToken, chainId, orderId, content: null, ct);

    public Task<FoodoraHttpResult> PutOrderStatusAsync(
        string accessToken,
        string chainId,
        string orderId,
        string status,
        CancellationToken ct = default)
    {
        if (status is not ("READY_FOR_PICKUP" or "DISPATCHED" or "CANCELLED"))
            return Task.FromResult(FoodoraHttpResult.Fail(StatusCodes.Status502BadGateway));

        var json = status == "CANCELLED"
            ? """{"status":"CANCELLED","reason":"VENDOR"}"""
            : "{\"status\":\"" + status + "\"}";
        return SendOrderAsync(
            HttpMethod.Put,
            accessToken,
            chainId,
            orderId,
            new StringContent(json, Encoding.UTF8, "application/json"),
            ct);
    }

    private async Task<FoodoraHttpResult> SendOrderAsync(
        HttpMethod method,
        string accessToken,
        string chainId,
        string orderId,
        HttpContent? content,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken)
            || string.IsNullOrWhiteSpace(chainId)
            || string.IsNullOrWhiteSpace(orderId))
            return FoodoraHttpResult.Fail(StatusCodes.Status502BadGateway);

        var baseUrl = (_options.Value.Foodora.ApiBaseUrl ?? string.Empty).TrimEnd('/');
        if (!baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return FoodoraHttpResult.Fail(StatusCodes.Status502BadGateway);

        var url = baseUrl
            + "/v2/chains/"
            + Uri.EscapeDataString(chainId.Trim())
            + "/orders/"
            + Uri.EscapeDataString(orderId.Trim());
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;
        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Foodora order {Method} returned {StatusCode}.", method.Method, (int)response.StatusCode);
                return FoodoraHttpResult.Fail((int)response.StatusCode);
            }

            var body = method == HttpMethod.Get
                ? await response.Content.ReadAsStringAsync(ct)
                : string.Empty;
            return FoodoraHttpResult.Ok(body);
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Foodora order {Method} failed.", method.Method);
            return FoodoraHttpResult.Fail(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static string? ReadString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}

public sealed class FoodoraAccessTokenSource : IFoodoraAccessTokenSource
{
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(60);

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly OnlineOrderIntegrationTokenProtector _protector;
    private readonly IFoodoraAuthClient _auth;
    private readonly TimeProvider _time;
    private readonly ILogger<FoodoraAccessTokenSource> _logger;

    public FoodoraAccessTokenSource(
        IDbContextFactory<AppDbContext> dbFactory,
        OnlineOrderIntegrationTokenProtector protector,
        IFoodoraAuthClient auth,
        TimeProvider time,
        ILogger<FoodoraAccessTokenSource> logger)
    {
        _dbFactory = dbFactory;
        _protector = protector;
        _auth = auth;
        _time = time;
        _logger = logger;
    }

    public async Task<FoodoraAccess?> GetAccessAsync(Guid tenantId, CancellationToken ct = default)
    {
        var gate = Gates.GetOrAdd(tenantId.ToString("N"), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var row = await db.OnlineOrderIntegrationTenantCredentials
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    c => c.TenantId == tenantId
                         && c.Provider == OnlineOrderIntegrationSignatureStatus.FoodoraProvider
                         && c.IsActive,
                    ct);
            if (row is null || string.IsNullOrWhiteSpace(row.ChainId))
                return null;

            string clientId;
            string clientSecret;
            try
            {
                clientId = _protector.Unprotect(row.ClientId);
                clientSecret = _protector.Unprotect(row.ClientSecret);
            }
            catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
            {
                _logger.LogWarning("Foodora tenant credentials could not be decrypted.");
                return null;
            }

            var now = _time.GetUtcNow().UtcDateTime;
            if (!string.IsNullOrWhiteSpace(row.AccessToken)
                && row.AccessTokenExpiresAt is DateTime expiresAt
                && expiresAt > now.Add(Skew))
            {
                try
                {
                    return new FoodoraAccess
                    {
                        AccessToken = _protector.Unprotect(row.AccessToken),
                        ChainId = row.ChainId,
                    };
                }
                catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
                {
                    _logger.LogWarning("Foodora access token could not be decrypted.");
                }
            }

            var granted = await _auth.RequestClientCredentialsAsync(clientId, clientSecret, ct);
            if (!granted.Succeeded || string.IsNullOrWhiteSpace(granted.AccessToken))
            {
                _logger.LogWarning("Foodora token request failed with {StatusCode}.", granted.StatusCode);
                return null;
            }

            row.AccessToken = _protector.Protect(granted.AccessToken);
            row.AccessTokenExpiresAt = granted.ExpiresAtUtc;
            row.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return new FoodoraAccess
            {
                AccessToken = granted.AccessToken,
                ChainId = row.ChainId,
            };
        }
        finally
        {
            gate.Release();
        }
    }
}
