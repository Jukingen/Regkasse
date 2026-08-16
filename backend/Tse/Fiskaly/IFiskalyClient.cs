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

    Task<FiskalyAuthResult> AuthenticateAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("AuthenticateAsync is not implemented by this IFiskalyClient.");

    Task<FiskalyScuInfo> CreateSignatureCreationUnitAsync(
        Guid signatureCreationUnitId,
        string vatId,
        string? legalEntityName = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("CreateSignatureCreationUnitAsync is not implemented by this IFiskalyClient.");

    Task<FiskalyCashRegisterInfo> CreateCashRegisterAsync(
        Guid cashRegisterId,
        string description,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("CreateCashRegisterAsync is not implemented by this IFiskalyClient.");

    Task<FiskalyCashRegisterInfo?> GetCashRegisterAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("GetCashRegisterAsync is not implemented by this IFiskalyClient.");

    Task<FiskalySignedReceipt> SignReceiptAsync(
        Guid cashRegisterId,
        Guid receiptId,
        FiskalyTransactionData data,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("SignReceiptAsync is not implemented by this IFiskalyClient.");
}

public sealed record FiskalyScuInfo(
    string Id,
    string State,
    string? CertificateSerialNumber);
