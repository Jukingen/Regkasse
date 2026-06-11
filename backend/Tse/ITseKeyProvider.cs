using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// TSE imza anahtarı sağlayıcı. Production'da TSE cihazından, testte yazılım anahtarı.
    /// </summary>
    public interface ITseKeyProvider
    {
        /// <summary>İmza için ECDsa P-256 anahtarı (private key ile).</summary>
        ECDsa GetSigningKey();

        /// <summary>Sertifika bytes (DER) — doğrulama ve DEP export için.</summary>
        byte[]? GetCertificateBytes();

        /// <summary>Sertifika seri numarası.</summary>
        string? GetCertificateSerialNumber();

        /// <summary>Active signing certificate thumbprint (SHA-1, uppercase).</summary>
        string? GetCurrentCertificateThumbprint();

        /// <summary>Resolves a signing certificate previously registered for DEP export grouping.</summary>
        Task<X509Certificate2?> GetCertificateByThumbprintAsync(
            string thumbprint,
            CancellationToken cancellationToken = default);

        /// <summary>Issuer CA certificates for the leaf signing cert (excludes leaf).</summary>
        Task<IReadOnlyList<X509Certificate2>> GetCertificateChainAsync(
            string thumbprint,
            CancellationToken cancellationToken = default);

        /// <summary>AES-256 key bytes for RKSV turnover counter encryption (32 bytes).</summary>
        byte[]? GetTurnoverCounterAesKeyBytes();
    }
}
