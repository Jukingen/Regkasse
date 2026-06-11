using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Tests.Fixtures;

/// <summary>
/// Deterministic software TSE for BMF Prüftool fixture generation (not for production).
/// </summary>
internal sealed class FixedPrueftoolTseKeyProvider : ITseKeyProvider
{
    /// <summary>Fixed P-256 PKCS#8 (generated once for Prüftool fixtures).</summary>
    private const string FixturePkcs8PrivateKeyBase64 =
        "MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgNopuHvyV6gpKn82AsboQuse5OFRyKDXjPuMv+MI4JmqhRANCAARwWXRtyP6cl9XMyt/CrIV15vqysMQEYVEDejfS+9R7rkKltGpPnqgZTltEcMXBHv26oAkmE4M2gfU0GRrmxT75";

    private static readonly byte[] FixtureTurnoverAesKey =
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("Regkasse.Prueftool.Fixture.AesKey.v1"));

    private readonly ECDsa _key;
    private readonly SigningCertificateBundle _bundle;
    private readonly ConcurrentDictionary<string, SigningCertificateBundle> _registry =
        new(StringComparer.OrdinalIgnoreCase);

    public FixedPrueftoolTseKeyProvider()
    {
        _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pkcs8 = Convert.FromBase64String(FixturePkcs8PrivateKeyBase64);
        _key.ImportPkcs8PrivateKey(pkcs8, out _);
        _bundle = CreateSigningBundle();
        _registry[_bundle.Thumbprint] = _bundle;
    }

    public ECDsa GetSigningKey() => _key;

    public byte[]? GetCertificateBytes() => _bundle.DerBytes;

    public string? GetCertificateSerialNumber() => _bundle.SerialNumber;

    public string? GetCurrentCertificateThumbprint() => _bundle.Thumbprint;

    public Task<X509Certificate2?> GetCertificateByThumbprintAsync(
        string thumbprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_registry.TryGetValue(thumbprint.Trim(), out var bundle))
            return Task.FromResult<X509Certificate2?>(bundle.Certificate);
        return Task.FromResult<X509Certificate2?>(null);
    }

    public Task<IReadOnlyList<X509Certificate2>> GetCertificateChainAsync(
        string thumbprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<X509Certificate2>>(Array.Empty<X509Certificate2>());
    }

    public byte[]? GetTurnoverCounterAesKeyBytes() => FixtureTurnoverAesKey;

    public ECDsa GetPublicKey() => _key;

    private SigningCertificateBundle CreateSigningBundle()
    {
        var request = new CertificateRequest(
            "CN=Regkasse Prueftool Fixture",
            _key,
            HashAlgorithmName.SHA256);

        var cert = request.CreateSelfSigned(
            new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2036, 6, 11, 0, 0, 0, TimeSpan.Zero));

        var thumbprint = TseCertificateThumbprint.Compute(cert);
        var serial = cert.SerialNumber.TrimStart('0').ToUpperInvariant();
        if (string.IsNullOrEmpty(serial))
            serial = "PRUEFTOOL-FIXTURE-01";

        return new SigningCertificateBundle(cert, thumbprint, serial, cert.RawData);
    }

    private sealed record SigningCertificateBundle(
        X509Certificate2 Certificate,
        string Thumbprint,
        string SerialNumber,
        byte[] DerBytes);
}
