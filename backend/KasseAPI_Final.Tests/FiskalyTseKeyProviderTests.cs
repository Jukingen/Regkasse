using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Tse;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyTseKeyProviderTests
{
    private const string ScuId = "1c81cb86-c2e8-4074-afc3-a0601b2bf063";

    [Fact]
    public void GetCertificateBytes_ReturnsConfiguredDer()
    {
        var software = new SoftwareTseKeyProvider();
        var der = software.GetCertificateBytes()!;
        var cert = X509CertificateLoader.LoadCertificate(der);
        var thumbprint = TseCertificateThumbprint.Compute(cert);

        var client = new StubFiskalyClient(cert, thumbprint, "A1B2C3");
        var provider = CreateProvider(client, der);

        Assert.NotNull(provider.GetCertificateBytes());
        Assert.Equal(der, provider.GetCertificateBytes());
        Assert.Equal(cert.SerialNumber.TrimStart('0').ToUpperInvariant(), provider.GetCertificateSerialNumber());
        Assert.Equal(thumbprint, provider.GetCurrentCertificateThumbprint());
    }

    [Fact]
    public async Task GetCertificateByThumbprintAsync_ResolvesFromClient()
    {
        var software = new SoftwareTseKeyProvider();
        var der = software.GetCertificateBytes()!;
        var cert = X509CertificateLoader.LoadCertificate(der);
        var thumbprint = TseCertificateThumbprint.Compute(cert);

        var client = new StubFiskalyClient(cert, thumbprint, "SER-1");
        var provider = CreateProvider(client, der);

        var resolved = await provider.GetCertificateByThumbprintAsync(thumbprint);
        Assert.NotNull(resolved);
        Assert.Equal(thumbprint, TseCertificateThumbprint.Compute(resolved!));
    }

    [Fact]
    public void GetSigningKey_ReturnsDelegatedEcdsa()
    {
        var software = new SoftwareTseKeyProvider();
        var der = software.GetCertificateBytes()!;
        var cert = X509CertificateLoader.LoadCertificate(der);
        var thumbprint = TseCertificateThumbprint.Compute(cert);

        var client = new StubFiskalyClient(cert, thumbprint, "SER-1", signHash: hash => software.GetSigningKey().SignHash(hash));
        var provider = CreateProvider(client, der);

        var key = provider.GetSigningKey();
        var digest = SHA256.HashData("fiskaly-sign-test"u8.ToArray());
        var signature = key.SignHash(digest);

        Assert.NotEmpty(signature);
        Assert.True(key.VerifyHash(digest, signature));
    }

    private static FiskalyTseKeyProvider CreateProvider(StubFiskalyClient client, byte[] certDer)
    {
        var options = Options.Create(new FiskalyOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            ApiSecret = "test-secret",
            SignatureCreationUnitId = ScuId,
            SigningCertificateDerBase64 = Convert.ToBase64String(certDer),
            TurnoverCounterAesKeyBase64 = Convert.ToBase64String(new byte[32]),
        });

        return new FiskalyTseKeyProvider(client, options);
    }

    private sealed class StubFiskalyClient : IFiskalyClient
    {
        private readonly X509Certificate2 _certificate;
        private readonly string _thumbprint;
        private readonly string _serial;
        private readonly Func<byte[], byte[]>? _signHash;

        public StubFiskalyClient(
            X509Certificate2 certificate,
            string thumbprint,
            string serial,
            Func<byte[], byte[]>? signHash = null)
        {
            _certificate = certificate;
            _thumbprint = thumbprint;
            _serial = serial;
            _signHash = signHash;
        }

        public Task<ECDsa> GetSigningKeyAsync(string signatureCreationUnitId, CancellationToken cancellationToken = default)
        {
            var verifyKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            verifyKey.ImportSubjectPublicKeyInfo(_certificate.PublicKey.ExportSubjectPublicKeyInfo(), out _);
            return Task.FromResult<ECDsa>(new FiskalyDelegatedSigningEcdsa(this, signatureCreationUnitId, verifyKey));
        }

        public Task<X509Certificate2?> GetCertificateAsync(string signatureCreationUnitId, CancellationToken cancellationToken = default) =>
            Task.FromResult<X509Certificate2?>(_certificate);

        public Task<X509Certificate2?> GetCertificateByThumbprintAsync(string thumbprint, CancellationToken cancellationToken = default) =>
            Task.FromResult(
                string.Equals(thumbprint, _thumbprint, StringComparison.OrdinalIgnoreCase)
                    ? _certificate
                    : null);

        public Task<IReadOnlyList<X509Certificate2>> GetCertificateChainAsync(
            string thumbprint,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<X509Certificate2>>(Array.Empty<X509Certificate2>());

        public Task<byte[]> SignSha256HashAsync(
            byte[] hash,
            string signatureCreationUnitId,
            CancellationToken cancellationToken = default)
        {
            if (_signHash == null)
                throw new InvalidOperationException("Stub signing not configured.");

            return Task.FromResult(_signHash(hash));
        }

        public Task<FiskalyScuInfo?> GetSignatureCreationUnitAsync(
            string signatureCreationUnitId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<FiskalyScuInfo?>(new FiskalyScuInfo(signatureCreationUnitId, "INITIALIZED", _serial));
    }
}
