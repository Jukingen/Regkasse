using System.Security.Cryptography.X509Certificates;

namespace KasseAPI_Final.Tse;

/// <summary>Builds BMF DEP <c>Zertifizierungsstellen</c> (issuer CA chain, excluding the leaf signing cert).</summary>
public static class TseCertificateChainBuilder
{
    public static IReadOnlyList<X509Certificate2> BuildIssuerChain(X509Certificate2 leaf)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

        if (!chain.Build(leaf))
            return Array.Empty<X509Certificate2>();

        var issuers = new List<X509Certificate2>();
        for (var i = 1; i < chain.ChainElements.Count; i++)
        {
            issuers.Add(chain.ChainElements[i].Certificate);
        }

        return issuers;
    }

    public static IReadOnlyList<string> ToBase64DerList(IEnumerable<X509Certificate2> certificates) =>
        certificates
            .Select(c => Convert.ToBase64String(c.Export(X509ContentType.Cert)))
            .ToList();
}
