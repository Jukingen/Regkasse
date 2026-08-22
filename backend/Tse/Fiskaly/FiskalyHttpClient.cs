using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Tse.Fiskaly;

/// <summary>
/// fiskaly SIGN AT REST client (auth + SCU metadata). Certificate material is loaded from
/// <see cref="FiskalyOptions.SigningCertificateDerBase64"/> because the public API does not return DER.
/// </summary>
public sealed class FiskalyHttpClient : IFiskalyClient
{
    private readonly HttpClient _httpClient;
    private readonly FiskalyOptions _options;
    private readonly FiskalyAccessTokenCache _tokenCache;
    private readonly FiskalyEnabledOverrideCache? _enabledCache;
    private readonly ILogger<FiskalyHttpClient> _logger;
    private readonly ConcurrentDictionary<string, SigningCertificateBundle> _registry =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _authLock = new(1, 1);

    private static readonly JsonSerializerOptions FiskalyJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public FiskalyHttpClient(
        HttpClient httpClient,
        IOptions<FiskalyOptions> options,
        FiskalyAccessTokenCache tokenCache,
        ILogger<FiskalyHttpClient> logger,
        FiskalyEnabledOverrideCache? enabledCache = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _tokenCache = tokenCache;
        _enabledCache = enabledCache;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
            _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        RegisterConfiguredCertificates();
    }

    public bool IsEnabled => _options.IsEffectivelyEnabled(_enabledCache?.OverrideEnabled);

    public async Task<ECDsa> GetSigningKeyAsync(string signatureCreationUnitId, CancellationToken cancellationToken = default)
    {
        var bundle = await ResolveActiveBundleAsync(signatureCreationUnitId, cancellationToken);
        var verifyKey = CreateVerifyKey(bundle.Certificate);
        return new FiskalyDelegatedSigningEcdsa(this, signatureCreationUnitId, verifyKey);
    }

    public async Task<X509Certificate2?> GetCertificateAsync(
        string signatureCreationUnitId,
        CancellationToken cancellationToken = default)
    {
        var bundle = await ResolveActiveBundleAsync(signatureCreationUnitId, cancellationToken);
        return bundle.Certificate;
    }

    public Task<X509Certificate2?> GetCertificateByThumbprintAsync(
        string thumbprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(thumbprint))
            return Task.FromResult<X509Certificate2?>(null);

        return Task.FromResult(
            _registry.TryGetValue(thumbprint.Trim(), out var bundle)
                ? bundle.Certificate
                : null);
    }

    public Task<IReadOnlyList<X509Certificate2>> GetCertificateChainAsync(
        string thumbprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(thumbprint))
            return Task.FromResult<IReadOnlyList<X509Certificate2>>(Array.Empty<X509Certificate2>());

        if (!_registry.TryGetValue(thumbprint.Trim(), out var bundle))
            return Task.FromResult<IReadOnlyList<X509Certificate2>>(Array.Empty<X509Certificate2>());

        if (bundle.IssuerCertificates.Count > 0)
            return Task.FromResult<IReadOnlyList<X509Certificate2>>(bundle.IssuerCertificates);

        return Task.FromResult(TseCertificateChainBuilder.BuildIssuerChain(bundle.Certificate));
    }

    public async Task<byte[]> SignSha256HashAsync(
        byte[] hash,
        string signatureCreationUnitId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hash);
        if (hash.Length != 32)
            throw new ArgumentException("SHA-256 hash must be 32 bytes.", nameof(hash));

        await EnsureAuthenticatedAsync(cancellationToken);

        // SIGN AT signs at receipt level (PUT /cash-register/.../receipt/...).
        // Low-level hash signing is not exposed on the public RKSV v1 API; this hook exists for
        // future fiskaly middleware or on-premise TSE bridges that implement raw ES256 signing.
        _logger.LogWarning(
            "fiskaly SIGN AT does not expose raw hash signing for SCU {ScuId}. " +
            "Use receipt-level fiscalization or a signing bridge.",
            signatureCreationUnitId);

        throw new FiskalyApiException(
            "fiskaly SIGN AT does not expose raw hash signing. Use SignReceiptAsync " +
            "(PUT /cash-register/{id}/receipt/{id}). Local RKSV compact JWS remains SignaturePipeline.");
    }

    public async Task<FiskalyScuInfo?> GetSignatureCreationUnitAsync(
        string signatureCreationUnitId,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation("Fiskaly is disabled — returning mock SCU {ScuId}", signatureCreationUnitId);
            return MockScu(signatureCreationUnitId);
        }

        await EnsureAuthenticatedAsync(cancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"signature-creation-unit/{signatureCreationUnitId}");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await RequireAccessTokenAsync(cancellationToken));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "fiskaly SCU retrieve failed for {ScuId}: {StatusCode}",
                signatureCreationUnitId,
                (int)response.StatusCode);
            return null;
        }

        var dto = await response.Content.ReadFromJsonAsync<FiskalyScuResponseDto>(
            cancellationToken: cancellationToken);

        if (dto == null)
            return null;

        return new FiskalyScuInfo(dto.Id ?? signatureCreationUnitId, dto.State ?? "UNKNOWN", dto.CertificateSerialNumber);
    }

    public async Task<FiskalyAuthResult> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogWarning("Fiskaly is disabled — skipping authentication.");
            return new FiskalyAuthResult(false, null, 0);
        }

        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        if (!_tokenCache.TryGet(out var token, out var expiresAt))
            throw new FiskalyApiException("fiskaly authentication succeeded but the token cache is empty.");

        _logger.LogInformation(
            "fiskaly authentication succeeded, tokenLength={TokenLength}, expiresAt={ExpiresAt}",
            token.Length,
            expiresAt);
        return new FiskalyAuthResult(true, expiresAt, token.Length);
    }

    public async Task<FiskalyScuInfo> CreateSignatureCreationUnitAsync(
        Guid signatureCreationUnitId,
        string vatId,
        string? legalEntityName = null,
        CancellationToken cancellationToken = default)
    {
        if (signatureCreationUnitId == Guid.Empty)
            throw new ArgumentException("SCU id must be a UUIDv4.", nameof(signatureCreationUnitId));
        if (string.IsNullOrWhiteSpace(vatId))
            throw new ArgumentException("VAT id is required to create an SCU.", nameof(vatId));

        if (!IsEnabled)
        {
            _logger.LogInformation("Fiskaly is disabled — returning mock SCU {ScuId}", signatureCreationUnitId);
            return MockScu(signatureCreationUnitId.ToString("D"));
        }

        var token = await RequireAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"signature-creation-unit/{signatureCreationUnitId:D}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            legal_entity_id = new { vat_id = vatId.Trim().ToUpperInvariant() },
            legal_entity_name = string.IsNullOrWhiteSpace(legalEntityName)
                ? "Regkasse"
                : legalEntityName.Trim()
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var existing = await GetSignatureCreationUnitAsync(signatureCreationUnitId.ToString("D"), cancellationToken)
                .ConfigureAwait(false);
            if (existing != null)
                return existing;
        }

        await EnsureSuccessAsync(response, "Create SCU", cancellationToken).ConfigureAwait(false);
        var dto = await response.Content.ReadFromJsonAsync<FiskalyScuResponseDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return new FiskalyScuInfo(
            dto?.Id ?? signatureCreationUnitId.ToString("D"),
            dto?.State ?? "CREATED",
            dto?.CertificateSerialNumber);
    }

    public async Task<FiskalyCashRegisterInfo> CreateCashRegisterAsync(
        Guid cashRegisterId,
        string description,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty)
            throw new ArgumentException("Cash register id must be a UUIDv4.", nameof(cashRegisterId));

        if (!IsEnabled)
        {
            _logger.LogInformation("Fiskaly is disabled — returning mock cash register {CashRegisterId}", cashRegisterId);
            return MockCashRegister(cashRegisterId.ToString("D"));
        }

        var token = await RequireAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"cash-register/{cashRegisterId:D}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            description = string.IsNullOrWhiteSpace(description) ? "Regkasse POS" : description.Trim()
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var existing = await GetCashRegisterAsync(cashRegisterId, cancellationToken).ConfigureAwait(false);
            if (existing != null)
                return existing;
        }

        await EnsureSuccessAsync(response, "Create cash register", cancellationToken).ConfigureAwait(false);
        var dto = await response.Content.ReadFromJsonAsync<FiskalyResourceResponseDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return new FiskalyCashRegisterInfo(
            dto?.Id ?? cashRegisterId.ToString("D"),
            dto?.State ?? "CREATED");
    }

    public async Task<FiskalyCashRegisterInfo?> GetCashRegisterAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation("Fiskaly is disabled — returning mock cash register {CashRegisterId}", cashRegisterId);
            return MockCashRegister(cashRegisterId.ToString("D"));
        }

        var token = await RequireAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"cash-register/{cashRegisterId:D}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        var dto = await response.Content.ReadFromJsonAsync<FiskalyResourceResponseDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (dto == null)
            return null;

        return new FiskalyCashRegisterInfo(dto.Id ?? cashRegisterId.ToString("D"), dto.State ?? "UNKNOWN");
    }

    public async Task<FiskalySignedReceipt> SignReceiptAsync(
        Guid cashRegisterId,
        Guid receiptId,
        FiskalyTransactionData data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (cashRegisterId == Guid.Empty)
            throw new ArgumentException("Cash register id is required.", nameof(cashRegisterId));
        if (receiptId == Guid.Empty)
            throw new ArgumentException("Receipt id is required.", nameof(receiptId));

        if (!IsEnabled)
            throw new FiskalyApiException("Fiskaly is disabled. Fiscal signing is not available.");

        var token = await RequireAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var payload = FiskalyReceiptSchemaMapper.BuildSignRequest(data);

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"cash-register/{cashRegisterId:D}/receipt/{receiptId:D}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(payload, options: FiskalyJson);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "Sign receipt", cancellationToken).ConfigureAwait(false);
        var dto = await response.Content.ReadFromJsonAsync<FiskalyReceiptResponseDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return MapReceipt(dto, cashRegisterId, receiptId);
    }

    public async Task<FiskalySignedReceipt> GetReceiptAsync(
        Guid cashRegisterId,
        string receiptIdOrNumber,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty)
            throw new ArgumentException("Cash register id is required.", nameof(cashRegisterId));
        if (string.IsNullOrWhiteSpace(receiptIdOrNumber))
            throw new ArgumentException("Receipt id or number is required.", nameof(receiptIdOrNumber));

        if (!IsEnabled)
            throw new FiskalyApiException("Fiskaly is disabled. Fiscal signing is not available.");

        var token = await RequireAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var encoded = Uri.EscapeDataString(receiptIdOrNumber.Trim());
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"cash-register/{cashRegisterId:D}/receipt/{encoded}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "Get receipt", cancellationToken).ConfigureAwait(false);
        var dto = await response.Content.ReadFromJsonAsync<FiskalyReceiptResponseDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        Guid? parsedReceiptId = Guid.TryParse(receiptIdOrNumber, out var id) ? id : null;
        return MapReceipt(dto, cashRegisterId, parsedReceiptId);
    }

    public async Task<FiskalyFonAuthResult> AuthenticateFonAsync(
        FiskalyFonAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsEnabled)
        {
            _logger.LogWarning("Fiskaly is disabled — skipping FON authentication.");
            return new FiskalyFonAuthResult(
                false,
                null,
                null,
                "NOT_AUTHENTICATED",
                null,
                "Fiskaly is disabled.");
        }

        var token = await RequireAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Put, "fon/auth");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = JsonContent.Create(new FiskalyFonAuthRequestDto
        {
            FonParticipantId = request.FonParticipantId.Trim(),
            FonUserId = request.FonUserId.Trim(),
            FonUserPin = request.FonUserPin
        });

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await ReadBodyPreviewAsync(response, cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("fiskaly FON authenticate failed HTTP {Status}", (int)response.StatusCode);
            return new FiskalyFonAuthResult(
                false,
                request.FonParticipantId.Trim(),
                request.FonUserId.Trim(),
                "NOT_AUTHENTICATED",
                null,
                SanitizeFonError(body, response.StatusCode));
        }

        var dto = await response.Content
            .ReadFromJsonAsync<FiskalyFonAuthResponseDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return MapFonAuth(dto, request.FonParticipantId.Trim(), request.FonUserId.Trim());
    }

    public async Task<FiskalyFonAuthResult> GetFonAuthStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return new FiskalyFonAuthResult(false, null, null, "NOT_AUTHENTICATED", null, "Fiskaly is disabled.");
        }

        var token = await RequireAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, "fon/auth");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return new FiskalyFonAuthResult(
                false,
                null,
                null,
                "UNKNOWN",
                null,
                SanitizeFonError(await ReadBodyPreviewAsync(response, cancellationToken).ConfigureAwait(false), response.StatusCode));
        }

        var dto = await response.Content
            .ReadFromJsonAsync<FiskalyFonAuthResponseDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return MapFonAuth(dto, dto?.FonParticipantId, dto?.FonUserId);
    }

    public async Task<FiskalyScuInfo> UpdateSignatureCreationUnitStateAsync(
        string signatureCreationUnitId,
        string state,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(signatureCreationUnitId))
            throw new ArgumentException("SCU id is required.", nameof(signatureCreationUnitId));
        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("State is required.", nameof(state));
        if (!IsEnabled)
            throw new FiskalyApiException("Fiskaly is disabled. SCU initialization is not available.");

        var token = await RequireAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"signature-creation-unit/{signatureCreationUnitId.Trim()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { state = state.Trim().ToUpperInvariant() });

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "Update SCU", cancellationToken).ConfigureAwait(false);
        var dto = await response.Content.ReadFromJsonAsync<FiskalyScuResponseDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return new FiskalyScuInfo(
            dto?.Id ?? signatureCreationUnitId.Trim(),
            dto?.State ?? state.Trim().ToUpperInvariant(),
            dto?.CertificateSerialNumber);
    }

    public async Task<FiskalyCashRegisterInfo> UpdateCashRegisterStateAsync(
        Guid cashRegisterId,
        string state,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty)
            throw new ArgumentException("Cash register id is required.", nameof(cashRegisterId));
        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("State is required.", nameof(state));
        if (!IsEnabled)
            throw new FiskalyApiException("Fiskaly is disabled. Cash register initialization is not available.");

        var token = await RequireAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"cash-register/{cashRegisterId:D}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { state = state.Trim().ToUpperInvariant() });

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "Update cash register", cancellationToken).ConfigureAwait(false);
        var dto = await response.Content.ReadFromJsonAsync<FiskalyResourceResponseDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return new FiskalyCashRegisterInfo(
            dto?.Id ?? cashRegisterId.ToString("D"),
            dto?.State ?? state.Trim().ToUpperInvariant());
    }

    private static FiskalyFonAuthResult MapFonAuth(
        FiskalyFonAuthResponseDto? dto,
        string? fallbackParticipant,
        string? fallbackUser)
    {
        var status = string.IsNullOrWhiteSpace(dto?.AuthenticationStatus)
            ? "UNKNOWN"
            : dto.AuthenticationStatus.Trim().ToUpperInvariant();
        var authenticated = string.Equals(status, FiskalyResourceStates.Authenticated, StringComparison.OrdinalIgnoreCase);
        DateTimeOffset? at = null;
        if (dto?.TimeAuthentication is long unix && unix > 0)
            at = DateTimeOffset.FromUnixTimeSeconds(unix);

        return new FiskalyFonAuthResult(
            authenticated,
            string.IsNullOrWhiteSpace(dto?.FonParticipantId) ? fallbackParticipant : dto.FonParticipantId,
            string.IsNullOrWhiteSpace(dto?.FonUserId) ? fallbackUser : dto.FonUserId,
            status,
            at);
    }

    private static string SanitizeFonError(string body, System.Net.HttpStatusCode status)
    {
        var preview = string.IsNullOrWhiteSpace(body) ? "(empty response)" : body;
        return $"FON request failed ({(int)status}): {preview}";
    }

    private static async Task<string> ReadBodyPreviewAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;
        var trimmed = body.Trim()
            .Replace("fon_user_pin", "***", StringComparison.OrdinalIgnoreCase)
            .Replace("api_secret", "***", StringComparison.OrdinalIgnoreCase)
            .Replace("api_key", "***", StringComparison.OrdinalIgnoreCase);
        return trimmed.Length <= 240 ? trimmed : trimmed[..240] + "…";
    }

    private async Task<SigningCertificateBundle> ResolveActiveBundleAsync(
        string signatureCreationUnitId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(signatureCreationUnitId))
            throw new InvalidOperationException("fiskaly Signature Creation Unit id is required.");

        if (!string.Equals(
                signatureCreationUnitId.Trim(),
                _options.SignatureCreationUnitId.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Requested SCU '{signatureCreationUnitId}' does not match configured SCU '{_options.SignatureCreationUnitId}'.");
        }

        var scu = await GetSignatureCreationUnitAsync(signatureCreationUnitId, cancellationToken);
        if (scu != null && !string.Equals(scu.State, "INITIALIZED", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("fiskaly SCU {ScuId} is in state {State}, expected INITIALIZED.", scu.Id, scu.State);
        }

        var active = _registry.Values.FirstOrDefault(b => b.IsActive)
            ?? throw new InvalidOperationException(
                "No fiskaly signing certificate configured. Set Fiskaly:SigningCertificateDerBase64.");

        if (!string.IsNullOrWhiteSpace(scu?.CertificateSerialNumber))
            active = active with { SerialNumber = scu.CertificateSerialNumber.Trim() };

        return active;
    }

    private void RegisterConfiguredCertificates()
    {
        if (string.IsNullOrWhiteSpace(_options.SigningCertificateDerBase64))
            return;

        var leafDer = Convert.FromBase64String(_options.SigningCertificateDerBase64.Trim());
        var leaf = X509CertificateLoader.LoadCertificate(leafDer);
        var thumbprint = TseCertificateThumbprint.Compute(leaf);
        var serial = leaf.SerialNumber.TrimStart('0').ToUpperInvariant();
        if (string.IsNullOrEmpty(serial))
            serial = "FISKALY-SCU";

        var issuers = new List<X509Certificate2>();
        foreach (var issuerB64 in _options.IssuerCertificatesDerBase64)
        {
            if (string.IsNullOrWhiteSpace(issuerB64))
                continue;

            try
            {
                var issuerDer = Convert.FromBase64String(issuerB64.Trim());
                issuers.Add(X509CertificateLoader.LoadCertificate(issuerDer));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping invalid fiskaly issuer certificate entry.");
            }
        }

        var bundle = new SigningCertificateBundle(leaf, thumbprint, serial, leafDer, issuers, IsActive: true);
        _registry[thumbprint] = bundle;
    }

    private async Task<string> RequireAccessTokenAsync(CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        if (_tokenCache.TryGet(out var token, out _))
            return token;

        throw new FiskalyApiException("fiskaly access token is not available.");
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
            throw new FiskalyApiException("Fiskaly is disabled.");

        if (_tokenCache.TryGet(out _, out _))
            return;

        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            throw new FiskalyApiException(
                "Fiskaly:ApiKey / Fiskaly:ApiSecret are not configured. Set user-secrets in Development.");
        }

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (_tokenCache.TryGet(out _, out _))
                return;

            var authBody = new FiskalyAuthRequestDto
            {
                ApiKey = _options.ApiKey,
                ApiSecret = _options.ApiSecret,
            };

            using var response = await PostAuthWithRetryAsync(authBody, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                await ThrowApiErrorAsync(response, "Authenticate", cancellationToken).ConfigureAwait(false);

            var auth = await response.Content.ReadFromJsonAsync<FiskalyAuthResponseDto>(
                cancellationToken: cancellationToken)
                ?? throw new FiskalyApiException("fiskaly auth returned empty body.");

            if (string.IsNullOrWhiteSpace(auth.AccessToken))
                throw new FiskalyApiException("fiskaly auth returned no access_token.");

            var expiresAt = auth.ExpiresAt ?? DateTimeOffset.UtcNow.Add(_options.ResolveTokenCacheLifetime());
            _tokenCache.Set(auth.AccessToken, expiresAt);
        }
        finally
        {
            _authLock.Release();
        }
    }

    private async Task<HttpResponseMessage> PostAuthWithRetryAsync(
        FiskalyAuthRequestDto authBody,
        CancellationToken cancellationToken)
    {
        var attempts = _options.ResolveMaxRetries();
        var delay = _options.ResolveRetryDelay();
        HttpResponseMessage? last = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            last?.Dispose();
            last = await _httpClient.PostAsJsonAsync("auth", authBody, cancellationToken).ConfigureAwait(false);
            if (!IsTransient(last) || attempt == attempts)
                return last;

            _logger.LogWarning(
                "fiskaly auth transient failure HTTP {Status} (attempt {Attempt}/{Attempts})",
                (int)last.StatusCode,
                attempt,
                attempts);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        return last!;
    }

    private static bool IsTransient(HttpResponseMessage response) =>
        (int)response.StatusCode == 408
        || (int)response.StatusCode == 429
        || (int)response.StatusCode >= 500;

    private static FiskalyScuInfo MockScu(string scuId) =>
        new(string.IsNullOrWhiteSpace(scuId) ? Guid.NewGuid().ToString("D") : scuId, "CREATED", null, IsMock: true);

    private static FiskalyCashRegisterInfo MockCashRegister(string cashRegisterId) =>
        new(cashRegisterId, "CREATED", IsMock: true);

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        await ThrowApiErrorAsync(response, operation, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ThrowApiErrorAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var truncated = string.IsNullOrWhiteSpace(body)
            ? "(empty response)"
            : (body.Length <= 400 ? body.Trim() : body.Trim()[..400] + "…");
        var requestId = response.Headers.TryGetValues("request-id", out var values)
            ? values.FirstOrDefault()
            : null;

        throw new FiskalyApiException(
            $"fiskaly {operation} failed ({(int)response.StatusCode}): {truncated}",
            response.StatusCode,
            requestId);
    }

    private static ECDsa CreateVerifyKey(X509Certificate2 certificate)
    {
        var verifyKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var spki = certificate.PublicKey.ExportSubjectPublicKeyInfo();
        verifyKey.ImportSubjectPublicKeyInfo(spki, out _);
        return verifyKey;
    }

    private sealed record SigningCertificateBundle(
        X509Certificate2 Certificate,
        string Thumbprint,
        string SerialNumber,
        byte[] DerBytes,
        IReadOnlyList<X509Certificate2> IssuerCertificates,
        bool IsActive);

    private sealed class FiskalyAuthRequestDto
    {
        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; } = string.Empty;

        [JsonPropertyName("api_secret")]
        public string ApiSecret { get; set; } = string.Empty;
    }

    private sealed class FiskalyAuthResponseDto
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTimeOffset? ExpiresAt { get; set; }
    }

    private sealed class FiskalyScuResponseDto
    {
        [JsonPropertyName("_id")]
        public string? Id { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("certificate_serial_number")]
        public string? CertificateSerialNumber { get; set; }
    }

    private sealed class FiskalyResourceResponseDto
    {
        [JsonPropertyName("_id")]
        public string? Id { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }
    }

    private static FiskalySignedReceipt MapReceipt(
        FiskalyReceiptResponseDto? dto,
        Guid cashRegisterId,
        Guid? receiptId)
    {
        var signed = dto?.Signed
            ?? string.Equals(dto?.State, "SIGNED", StringComparison.OrdinalIgnoreCase);
        var fonJson = dto?.FonValidations is { ValueKind: JsonValueKind.Undefined or JsonValueKind.Null }
            ? null
            : dto?.FonValidations?.GetRawText();
        return new FiskalySignedReceipt(
            dto?.Id ?? receiptId?.ToString("D") ?? string.Empty,
            dto?.CashRegisterId ?? cashRegisterId.ToString("D"),
            dto?.State ?? "UNKNOWN",
            dto?.QrCodeData,
            dto?.ReceiptNumber,
            dto?.Environment,
            dto?.TimeSignature,
            signed,
            dto?.CashRegisterSerialNumber,
            dto?.Hints,
            dto?.ReceiptType,
            fonJson);
    }

    private sealed class FiskalyReceiptResponseDto
    {
        [JsonPropertyName("_id")]
        public string? Id { get; set; }

        [JsonPropertyName("cash_register_id")]
        public string? CashRegisterId { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("_env")]
        public string? Environment { get; set; }

        [JsonPropertyName("qr_code_data")]
        public string? QrCodeData { get; set; }

        [JsonPropertyName("receipt_number")]
        public string? ReceiptNumber { get; set; }

        [JsonPropertyName("time_signature")]
        public long? TimeSignature { get; set; }

        [JsonPropertyName("signed")]
        public bool? Signed { get; set; }

        [JsonPropertyName("cash_register_serial_number")]
        public string? CashRegisterSerialNumber { get; set; }

        [JsonPropertyName("receipt_type")]
        public string? ReceiptType { get; set; }

        [JsonPropertyName("hints")]
        public List<string>? Hints { get; set; }

        [JsonPropertyName("fon_validations")]
        public JsonElement? FonValidations { get; set; }
    }

    private sealed class FiskalyFonAuthRequestDto
    {
        [JsonPropertyName("fon_participant_id")]
        public string FonParticipantId { get; set; } = string.Empty;

        [JsonPropertyName("fon_user_id")]
        public string FonUserId { get; set; } = string.Empty;

        [JsonPropertyName("fon_user_pin")]
        public string FonUserPin { get; set; } = string.Empty;
    }

    private sealed class FiskalyFonAuthResponseDto
    {
        [JsonPropertyName("fon_participant_id")]
        public string? FonParticipantId { get; set; }

        [JsonPropertyName("fon_user_id")]
        public string? FonUserId { get; set; }

        [JsonPropertyName("authentication_status")]
        public string? AuthenticationStatus { get; set; }

        [JsonPropertyName("time_authentication")]
        public long? TimeAuthentication { get; set; }
    }
}
