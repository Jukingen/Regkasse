using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// Yazılım tabanlı TSE anahtar sağlayıcı - test ve TSE cihazı olmadan geliştirme için.
    /// Production'da gerçek TSE cihazı entegrasyonu kullanılmalı.
    /// </summary>
    public class SoftwareTseKeyProvider : ITseKeyProvider
    {
        private readonly ECDsa _key;
        private readonly Lazy<SigningCertificateBundle> _bundle;
        private readonly ConcurrentDictionary<string, SigningCertificateBundle> _registry =
            new(StringComparer.OrdinalIgnoreCase);

        public SoftwareTseKeyProvider()
        {
            _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _bundle = new Lazy<SigningCertificateBundle>(CreateSigningBundle);
            RegisterBundle(_bundle.Value);
        }

        public ECDsa GetSigningKey() => _key;

        public byte[]? GetCertificateBytes() => _bundle.Value.DerBytes;

        public string? GetCertificateSerialNumber() => _bundle.Value.SerialNumber;

        public string? GetCurrentCertificateThumbprint() => _bundle.Value.Thumbprint;

        public Task<X509Certificate2?> GetCertificateByThumbprintAsync(
            string thumbprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(thumbprint))
                return Task.FromResult<X509Certificate2?>(null);

            if (_registry.TryGetValue(thumbprint.Trim(), out var bundle))
                return Task.FromResult<X509Certificate2?>(bundle.Certificate);

            return Task.FromResult<X509Certificate2?>(null);
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

            return Task.FromResult(TseCertificateChainBuilder.BuildIssuerChain(bundle.Certificate));
        }

        public byte[]? GetTurnoverCounterAesKeyBytes() => _turnoverAesKey;

        private static readonly byte[] _turnoverAesKey = DeriveDevTurnoverAesKey();

        /// <summary>Doğrulama için public key (aynı instance ile sign/verify test).</summary>
        public ECDsa GetPublicKey() => _key;

        private SigningCertificateBundle CreateSigningBundle()
        {
            var request = new CertificateRequest(
                "CN=Regkasse Software TSE Dev",
                _key,
                HashAlgorithmName.SHA256);

            var cert = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(5));

            var thumbprint = TseCertificateThumbprint.Compute(cert);
            var serial = cert.SerialNumber.TrimStart('0').ToUpperInvariant();
            if (string.IsNullOrEmpty(serial))
                serial = "SW-TEST-DEV";

            return new SigningCertificateBundle(cert, thumbprint, serial, cert.RawData);
        }

        private void RegisterBundle(SigningCertificateBundle bundle) =>
            _registry[bundle.Thumbprint] = bundle;

        private static byte[] DeriveDevTurnoverAesKey()
        {
            // Deterministic dev key for Prüftool / software TSE (production: FinanzOnline-registered AES key).
            return System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("Regkasse.SoftwareTse.TurnoverAesKey.v1"));
        }

        private sealed record SigningCertificateBundle(
            X509Certificate2 Certificate,
            string Thumbprint,
            string SerialNumber,
            byte[] DerBytes);
    }
}
