using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// RKSV Checklist 1: CMC / sertifika-anahtar eşleşmesi.
    /// Hata kodları: CMC_MISSING_KEY, CERT_MISMATCH
    /// </summary>
    public static class CmcParser
    {
        /// <summary>
        /// X.509 sertifikasından public key ve seri no çıkarır.
        /// CMC wrapper desteklenir (certificate-only CMS).
        /// </summary>
        public static CmcParseResult ParseCertificate(byte[] certData)
        {
            if (certData == null || certData.Length == 0)
                throw new TsePipelineException("CMC_MISSING_KEY", "Certificate data is empty");

            try
            {
                X509Certificate2 cert;
                var pem = Encoding.UTF8.GetString(certData);
                if (pem.Contains("-----BEGIN"))
                {
                    cert = X509Certificate2.CreateFromPem(pem);
                }
                else
                {
                    cert = new X509Certificate2(certData);
                }

                var publicKey = cert.GetECDsaPublicKey();
                if (publicKey == null)
                    throw new TsePipelineException("CMC_MISSING_KEY", "Certificate does not contain ECDSA public key");

                var serialNumber = cert.SerialNumber;
                if (string.IsNullOrEmpty(serialNumber))
                    throw new TsePipelineException("CERT_MISMATCH", "Certificate serial number is missing");

                return new CmcParseResult
                {
                    SerialNumber = serialNumber.TrimStart('0').ToUpperInvariant(),
                    PublicKey = publicKey,
                    ValidFrom = cert.NotBefore,
                    ValidUntil = cert.NotAfter,
                    IsValid = DateTime.UtcNow >= cert.NotBefore && DateTime.UtcNow <= cert.NotAfter
                };
            }
            catch (TsePipelineException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new TsePipelineException("CERT_MISMATCH", $"Certificate parsing failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// İmza public key'in sertifika public key ile eşleştiğini doğrular.
        /// </summary>
        public static void ValidateKeyMatch(ECDsa certPublicKey, ECDsa signingKey)
        {
            if (certPublicKey == null)
                throw new TsePipelineException("CMC_MISSING_KEY", "Certificate public key is null");
            if (signingKey == null)
                throw new TsePipelineException("CMC_MISSING_KEY", "Signing key is null");

            // Export parameters karşılaştırması
            var certParams = certPublicKey.ExportParameters(false);
            var signParams = signingKey.ExportParameters(false);
            if (!ByteArraysEqual(certParams.Q.X, signParams.Q.X) || !ByteArraysEqual(certParams.Q.Y, signParams.Q.Y))
                throw new TsePipelineException("CERT_MISMATCH", "Signing key does not match certificate public key");
        }

        private static bool ByteArraysEqual(byte[]? a, byte[]? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }

    public class CmcParseResult
    {
        public required string SerialNumber { get; init; }
        public required ECDsa PublicKey { get; init; }
        public DateTime ValidFrom { get; init; }
        public DateTime ValidUntil { get; init; }
        public bool IsValid { get; init; }
    }
}
