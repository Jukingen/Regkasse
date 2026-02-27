using System.Security.Cryptography;

namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// Yazılım tabanlı TSE anahtar sağlayıcı - test ve TSE cihazı olmadan geliştirme için.
    /// Production'da gerçek TSE cihazı entegrasyonu kullanılmalı.
    /// </summary>
    public class SoftwareTseKeyProvider : ITseKeyProvider
    {
        private readonly ECDsa _key;
        private readonly string _certSerialNumber;

        public SoftwareTseKeyProvider()
        {
            _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _certSerialNumber = "SW-TEST-" + Guid.NewGuid().ToString("N")[..8];
        }

        public ECDsa GetSigningKey() => _key;

        public byte[]? GetCertificateBytes() => null;

        public string? GetCertificateSerialNumber() => _certSerialNumber;

        /// <summary>
        /// Doğrulama için public key (aynı instance ile sign/verify test).
        /// </summary>
        public ECDsa GetPublicKey() => _key;
    }
}
