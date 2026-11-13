namespace KasseAPI_Final.Services.Countries.KassenSicherheit;

/// <summary>Neutral sign input. No vendor identifiers.</summary>
public sealed record KassenSicherheitSignRequest(
    Guid TenantId,
    string Payload,
    string? CashRegisterId = null);

/// <summary>Neutral sign output. <see cref="Provider"/> is a product id such as <c>soft</c> or <c>not-configured</c>.</summary>
public sealed record KassenSicherheitSignResult(
    bool Signed,
    string? Signature,
    string? Provider,
    string? SignatureAlgorithm = null,
    string? CertificateSerial = null);

/// <summary>TSE health. Not a vendor status document.</summary>
public sealed record KassenSicherheitStatusResult(
    bool Ready,
    string? State,
    string? Provider);

/// <summary>Certificate material as opaque strings (typically Base64 DER). No vendor certificate DTO.</summary>
public sealed record KassenSicherheitCertificateChainResult(
    string? Provider,
    IReadOnlyList<string> Certificates);

/// <summary>
/// DE KassenSicherheit (KassenSichV) TSE contract. Austrian RKSV does not call this type.
/// Vendor HTTP shapes stay on <see cref="IKassenSicherheitHttpClient"/>.
/// </summary>
public interface IKassenSicherheitService
{
    Task<KassenSicherheitSignResult> SignAsync(
        KassenSicherheitSignRequest request,
        CancellationToken cancellationToken = default);

    Task<KassenSicherheitStatusResult> GetStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<KassenSicherheitCertificateChainResult> GetCertificateChainAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
