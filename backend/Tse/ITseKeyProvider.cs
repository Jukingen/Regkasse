using System.Security.Cryptography;

namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// TSE imza anahtarı sağlayıcı. Production'da TSE cihazından, testte yazılım anahtarı.
    /// </summary>
    public interface ITseKeyProvider
    {
        /// <summary>
        /// İmza için ECDsa P-256 anahtarı (private key ile).
        /// </summary>
        ECDsa GetSigningKey();

        /// <summary>
        /// Sertifika bytes (CMC/X.509) - doğrulama için.
        /// </summary>
        byte[]? GetCertificateBytes();

        /// <summary>
        /// Sertifika seri numarası.
        /// </summary>
        string? GetCertificateSerialNumber();
    }
}
