using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Countries.KassenSicherheit;

/// <summary>
/// SIGN DE TEST/LIVE HTTP client. Does not use the Austrian <c>Fiskaly:</c> block.
/// ApiKey, ApiSecret, AdminPin, and bearer tokens are never written to logs.
/// </summary>
public sealed class FiskalyDeKassenSicherheitHttpClient : IKassenSicherheitHttpClient
{
    public const string ProviderId = "fiskaly-de";
    public const string DsfinvkApiBaseUrl = "https://dsfinvk.fiskaly.com/api/v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _http;
    private readonly IOptions<KassenSicherheitOptions> _options;
    private readonly ILogger<FiskalyDeKassenSicherheitHttpClient> _logger;
    private readonly object _tokenGate = new();
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAtUtc;

    public FiskalyDeKassenSicherheitHttpClient(
        HttpClient http,
        IOptions<KassenSicherheitOptions> options,
        ILogger<FiskalyDeKassenSicherheitHttpClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KassenSicherheitAuthResult> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        var cached = ReadCachedToken();
        if (cached != null)
            return cached;

        var opts = _options.Value;
        if (string.IsNullOrWhiteSpace(opts.ApiKey) || string.IsNullOrWhiteSpace(opts.ApiSecret))
            throw new InvalidOperationException("KassenSicherheit API credentials are not configured.");

        using var response = await SendAsync(
            HttpMethod.Post,
            "auth",
            new { api_key = opts.ApiKey, api_secret = opts.ApiSecret },
            bearer: null,
            cancellationToken).ConfigureAwait(false);

        var root = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        if (!root.TryGetProperty("access_token", out var tokenElement))
            throw new InvalidOperationException("SIGN DE auth response did not include an access token.");
        var token = tokenElement.GetString();
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("SIGN DE auth response did not include an access token.");

        var seconds = 300;
        if (TryReadInt(root, "access_token_expires_in", out var expiresIn) && expiresIn > 0)
            seconds = expiresIn;
        else if (TryReadInt(root, "expires_in", out var alt) && alt > 0)
            seconds = alt;

        var ttl = TimeSpan.FromSeconds(Math.Max(1, seconds - 30));
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
        lock (_tokenGate)
        {
            _accessToken = token;
            _accessTokenExpiresAtUtc = expiresAt;
        }

        _logger.LogInformation("SIGN DE auth succeeded. ExpiresInSeconds={ExpiresInSeconds}", seconds);
        return new KassenSicherheitAuthResult(token, expiresAt);
    }

    public async Task<KassenSicherheitTssResult> CreateAndInitializeTssAsync(
        KassenSicherheitCreateTssRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireId(request.TssId, nameof(request.TssId));

        await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        using (var created = await SendAsync(
            HttpMethod.Post,
            "tss",
            new { description = request.Description ?? string.Empty },
            bearer: true,
            cancellationToken).ConfigureAwait(false))
        {
            _ = created;
        }

        using var initialized = await SendAsync(
            HttpMethod.Patch,
            "tss/" + Uri.EscapeDataString(request.TssId),
            new { state = "INITIALIZED" },
            bearer: true,
            cancellationToken).ConfigureAwait(false);
        var root = await ReadJsonAsync(initialized, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("SIGN DE TSS initialized. TssId={TssId}", request.TssId);
        return new KassenSicherheitTssResult(request.TssId, ReadString(root, "state"));
    }

    public async Task<KassenSicherheitClientResult> CreateClientAsync(
        KassenSicherheitCreateClientRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireId(request.TssId, nameof(request.TssId));
        RequireId(request.ClientId, nameof(request.ClientId));
        await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        using var response = await SendAsync(
            HttpMethod.Post,
            "tss/" + Uri.EscapeDataString(request.TssId) + "/client",
            new { serial_number = request.SerialNumber, client_id = request.ClientId },
            bearer: true,
            cancellationToken).ConfigureAwait(false);
        var root = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "SIGN DE client created. TssId={TssId} ClientId={ClientId}",
            request.TssId,
            request.ClientId);
        return new KassenSicherheitClientResult(request.ClientId, ReadString(root, "state"));
    }

    public async Task<KassenSicherheitTransactionResult> StartTransactionAsync(
        KassenSicherheitStartTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await UpsertTransactionAsync(
            HttpMethod.Post,
            "tss/" + Uri.EscapeDataString(request.TssId) + "/tx",
            request.TssId,
            request.ClientId,
            request.TransactionId,
            request.TxRevision,
            state: "ACTIVE",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<KassenSicherheitTransactionResult> FinishTransactionAsync(
        KassenSicherheitFinishTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireId(request.TransactionId, nameof(request.TransactionId));
        return await UpsertTransactionAsync(
            HttpMethod.Patch,
            "tss/" + Uri.EscapeDataString(request.TssId) + "/tx/" + Uri.EscapeDataString(request.TransactionId),
            request.TssId,
            request.ClientId,
            request.TransactionId,
            request.TxRevision,
            state: "FINISHED",
            cancellationToken,
            body: request.Receipt is null ? null : BuildFinishBody(request)).ConfigureAwait(false);
    }

    public async Task<KassenSicherheitExportResult> ExportDsfinvkAsync(
        KassenSicherheitExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireId(request.ExportId, nameof(request.ExportId));
        if (request.EndDateUnix < request.StartDateUnix)
            throw new ArgumentOutOfRangeException(nameof(request.EndDateUnix));
        var format = string.IsNullOrWhiteSpace(request.Format) ? "tar" : request.Format.Trim();
        await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        object body = string.IsNullOrWhiteSpace(request.ClientId)
            ? new { start_date = request.StartDateUnix, end_date = request.EndDateUnix, format }
            : new { start_date = request.StartDateUnix, end_date = request.EndDateUnix, format, client_id = request.ClientId };
        var uri = new Uri(DsfinvkApiBaseUrl + "/exports/" + Uri.EscapeDataString(request.ExportId), UriKind.Absolute);
        using var response = await SendAsync(HttpMethod.Put, uri, body, bearer: true, cancellationToken)
            .ConfigureAwait(false);
        var root = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        var exportId = ReadString(root, "_id") ?? request.ExportId;
        _logger.LogInformation("DSFinV-K export requested. ExportId={ExportId}", request.ExportId);
        return new KassenSicherheitExportResult(true, exportId, ProviderId);
    }

    private static object BuildFinishBody(KassenSicherheitFinishTransactionRequest request)
    {
        var receipt = request.Receipt!.StandardV1.Receipt;
        return new
        {
            state = "FINISHED",
            client_id = request.ClientId,
            schema = new
            {
                standard_v1 = new
                {
                    receipt = new
                    {
                        receipt_type = receipt.ReceiptType,
                        amounts_per_vat_rate = receipt.AmountsPerVatRate
                            .Select(row => new { vat_rate = row.VatRate, amount = row.Amount })
                            .ToArray(),
                        amounts_per_payment_type = receipt.AmountsPerPaymentType
                            .Select(row => new
                            {
                                payment_type = row.PaymentType,
                                amount = row.Amount,
                                currency_code = row.CurrencyCode,
                            })
                            .ToArray(),
                    },
                },
            },
            metadata = new { belegnummer = request.Belegnummer ?? request.Receipt.Belegnummer },
        };
    }

    private async Task<KassenSicherheitTransactionResult> UpsertTransactionAsync(
        HttpMethod method,
        string relativePath,
        string tssId,
        string clientId,
        string transactionId,
        int txRevision,
        string state,
        CancellationToken cancellationToken,
        object? body = null)
    {
        RequireId(tssId, nameof(tssId));
        RequireId(clientId, nameof(clientId));
        RequireId(transactionId, nameof(transactionId));
        if (txRevision < 1)
            throw new ArgumentOutOfRangeException(nameof(txRevision));

        await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        using var response = await SendAsync(
            method,
            relativePath,
            body ?? new { state, client_id = clientId },
            bearer: true,
            cancellationToken).ConfigureAwait(false);
        var root = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "SIGN DE transaction upsert. TssId={TssId} TxId={TxId} State={State} TxRevision={TxRevision}",
            tssId,
            transactionId,
            state,
            txRevision);
        return new KassenSicherheitTransactionResult(
            Completed: true,
            TransactionId: transactionId,
            State: ReadString(root, "state") ?? state,
            TxRevision: TryReadInt(root, "latest_revision", out var rev) ? rev : txRevision,
            Signature: ReadSignature(root),
            Provider: ProviderId);
    }

    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        object? body,
        bool? bearer,
        CancellationToken cancellationToken) =>
        SendAsync(method, BuildUri(relativePath), body, bearer, cancellationToken);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        Uri uri,
        object? body,
        bool? bearer,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, uri);
        if (bearer == true)
        {
            var auth = ReadCachedToken()
                ?? throw new InvalidOperationException("SIGN DE bearer token is missing.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        }

        if (body != null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOptions),
                Encoding.UTF8,
                "application/json");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout());
        var response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            var providerCode = await ReadProviderCodeAsync(response, cancellationToken).ConfigureAwait(false);
            response.Dispose();
            _logger.LogWarning(
                "SIGN DE HTTP failed. Method={Method} Status={Status} ProviderCode={ProviderCode}",
                method.Method,
                status,
                providerCode);
            throw new KassenSicherheitHttpException(status, providerCode);
        }

        return response;
    }

    private Uri BuildUri(string relativePath)
    {
        var baseUrl = _options.Value.ApiBaseUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("KassenSicherheit:ApiBaseUrl is required.");
        return new Uri(baseUrl + "/" + relativePath.TrimStart('/'), UriKind.Absolute);
    }

    private TimeSpan RequestTimeout()
    {
        var seconds = _options.Value.HttpTimeoutSeconds;
        if (seconds < 3)
            seconds = 3;
        if (seconds > 5)
            seconds = 5;
        return TimeSpan.FromSeconds(seconds);
    }

    private KassenSicherheitAuthResult? ReadCachedToken()
    {
        lock (_tokenGate)
        {
            if (string.IsNullOrWhiteSpace(_accessToken) || _accessTokenExpiresAtUtc <= DateTimeOffset.UtcNow)
                return null;
            return new KassenSicherheitAuthResult(_accessToken, _accessTokenExpiresAtUtc);
        }
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
            return default;
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static string? ReadSignature(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("signature", out var signature))
            return null;
        if (signature.ValueKind == JsonValueKind.String)
            return signature.GetString();
        if (signature.ValueKind == JsonValueKind.Object && signature.TryGetProperty("value", out var nested))
            return nested.GetString();
        return null;
    }

    private static bool TryReadInt(JsonElement root, string name, out int value)
    {
        value = 0;
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(name, out var element)
            && element.TryGetInt32(out value);
    }

    private static async Task<string?> ReadProviderCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = response.Content == null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var direct = ReadString(root, "code");
            if (!string.IsNullOrWhiteSpace(direct))
                return direct;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object)
            {
                return ReadString(error, "code");
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static void RequireId(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", name);
    }
}
