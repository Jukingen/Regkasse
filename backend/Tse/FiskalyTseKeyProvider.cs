using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Tse;

/// <summary>
/// Hardware / cloud TSE key provider backed by fiskaly SIGN AT SCU.
/// Private keys never leave fiskaly; <see cref="GetSigningKey"/> returns a delegated <see cref="ECDsa"/>.
/// </summary>
public sealed class FiskalyTseKeyProvider : ITseKeyProvider
{
    private readonly IFiskalyClient _fiskalyClient;
    private readonly FiskalyOptions _options;
    private readonly string _signatureCreationUnitId;
    private readonly Lazy<ActiveSigningMaterial> _active;
    private readonly ConcurrentDictionary<string, X509Certificate2> _thumbprintCache =
        new(StringComparer.OrdinalIgnoreCase);

    public FiskalyTseKeyProvider(IFiskalyClient fiskalyClient, IOptions<FiskalyOptions> options)
    {
        _fiskalyClient = fiskalyClient;
        _options = options.Value;
        _signatureCreationUnitId = _options.SignatureCreationUnitId.Trim();
        _active = new Lazy<ActiveSigningMaterial>(LoadActiveSigningMaterial);
    }

    public ECDsa GetSigningKey() => _active.Value.SigningKey;

    public byte[]? GetCertificateBytes() => _active.Value.CertificateDer;

    public string? GetCertificateSerialNumber() => _active.Value.CertificateSerialNumber;

    public string? GetCurrentCertificateThumbprint() => _active.Value.Thumbprint;

    public async Task<X509Certificate2?> GetCertificateByThumbprintAsync(
        string thumbprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(thumbprint))
            return null;

        var normalized = thumbprint.Trim();
        if (_thumbprintCache.TryGetValue(normalized, out var cached))
            return cached;

        var cert = await _fiskalyClient.GetCertificateByThumbprintAsync(normalized, cancellationToken);
        if (cert != null)
            _thumbprintCache[normalized] = cert;

        return cert;
    }

    public Task<IReadOnlyList<X509Certificate2>> GetCertificateChainAsync(
        string thumbprint,
        CancellationToken cancellationToken = default) =>
        _fiskalyClient.GetCertificateChainAsync(thumbprint, cancellationToken);

    public byte[]? GetTurnoverCounterAesKeyBytes()
    {
        if (string.IsNullOrWhiteSpace(_options.TurnoverCounterAesKeyBase64))
            return null;

        try
        {
            var key = Convert.FromBase64String(_options.TurnoverCounterAesKeyBase64.Trim());
            return key.Length == 32 ? key : null;
        }
        catch
        {
            return null;
        }
    }

    private ActiveSigningMaterial LoadActiveSigningMaterial()
    {
        var cert = _fiskalyClient
            .GetCertificateAsync(_signatureCreationUnitId, CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            ?? throw new InvalidOperationException(
                $"fiskaly signing certificate not available for SCU '{_signatureCreationUnitId}'.");

        var signingKey = _fiskalyClient
            .GetSigningKeyAsync(_signatureCreationUnitId, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        var thumbprint = TseCertificateThumbprint.Compute(cert);
        _thumbprintCache[thumbprint] = cert;

        return new ActiveSigningMaterial(
            signingKey,
            cert.RawData,
            cert.SerialNumber.TrimStart('0').ToUpperInvariant(),
            thumbprint);
    }

    private sealed record ActiveSigningMaterial(
        ECDsa SigningKey,
        byte[] CertificateDer,
        string CertificateSerialNumber,
        string Thumbprint);
}
