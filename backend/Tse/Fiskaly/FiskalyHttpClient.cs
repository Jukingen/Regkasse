using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
    private readonly ILogger<FiskalyHttpClient> _logger;
    private readonly ConcurrentDictionary<string, SigningCertificateBundle> _registry =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _authLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public FiskalyHttpClient(
        HttpClient httpClient,
        IOptions<FiskalyOptions> options,
        ILogger<FiskalyHttpClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
            _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        RegisterConfiguredCertificates();
    }

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

        throw new InvalidOperationException(
            "fiskaly SIGN AT signs receipts via PUT /cash-register/{id}/receipt/{id}. " +
            "Raw JWS hash signing is not available on the public API.");
    }

    public async Task<FiskalyScuInfo?> GetSignatureCreationUnitAsync(
        string signatureCreationUnitId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"signature-creation-unit/{signatureCreationUnitId}");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

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

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_accessToken != null && DateTimeOffset.UtcNow < _tokenExpiresAt.AddMinutes(-1))
            return;

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken != null && DateTimeOffset.UtcNow < _tokenExpiresAt.AddMinutes(-1))
                return;

            var authBody = new FiskalyAuthRequestDto
            {
                ApiKey = _options.ApiKey,
                ApiSecret = _options.ApiSecret,
            };

            using var response = await _httpClient.PostAsJsonAsync("auth", authBody, cancellationToken);
            response.EnsureSuccessStatusCode();

            var auth = await response.Content.ReadFromJsonAsync<FiskalyAuthResponseDto>(
                cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("fiskaly auth returned empty body.");

            if (string.IsNullOrWhiteSpace(auth.AccessToken))
                throw new InvalidOperationException("fiskaly auth returned no access_token.");

            _accessToken = auth.AccessToken;
            _tokenExpiresAt = auth.ExpiresAt ?? DateTimeOffset.UtcNow.AddHours(1);
        }
        finally
        {
            _authLock.Release();
        }
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
}
