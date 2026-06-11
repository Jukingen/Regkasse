using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace KasseAPI_Final.Tse.Fiskaly;

/// <summary>
/// HTTP client surface for fiskaly SIGN AT SCU certificate and delegated signing operations.
/// </summary>
public interface IFiskalyClient
{
    /// <summary>Returns an <see cref="ECDsa"/> that delegates signing to the fiskaly SCU (private key not exportable).</summary>
    Task<ECDsa> GetSigningKeyAsync(string signatureCreationUnitId, CancellationToken cancellationToken = default);

    Task<X509Certificate2?> GetCertificateAsync(string signatureCreationUnitId, CancellationToken cancellationToken = default);

    Task<X509Certificate2?> GetCertificateByThumbprintAsync(string thumbprint, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<X509Certificate2>> GetCertificateChainAsync(
        string thumbprint,
        CancellationToken cancellationToken = default);

    /// <summary>Signs a SHA-256 hash with the SCU (ES256 / P-256 raw R||S).</summary>
    Task<byte[]> SignSha256HashAsync(
        byte[] hash,
        string signatureCreationUnitId,
        CancellationToken cancellationToken = default);

    Task<FiskalyScuInfo?> GetSignatureCreationUnitAsync(
        string signatureCreationUnitId,
        CancellationToken cancellationToken = default);
}

public sealed record FiskalyScuInfo(
    string Id,
    string State,
    string? CertificateSerialNumber);
