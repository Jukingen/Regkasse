using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace KasseAPI_Final.Tse;

/// <summary>SHA-1 thumbprint (uppercase, no separators) — matches <see cref="X509Certificate2.Thumbprint"/>.</summary>
public static class TseCertificateThumbprint
{
    public static string Compute(X509Certificate2 certificate) =>
        certificate.Thumbprint;

    public static string? ComputeFromDer(byte[]? derBytes)
    {
        if (derBytes is not { Length: > 0 })
            return null;

        try
        {
            using var cert = X509CertificateLoader.LoadCertificate(derBytes);
            return Compute(cert);
        }
        catch
        {
            return null;
        }
    }
}
