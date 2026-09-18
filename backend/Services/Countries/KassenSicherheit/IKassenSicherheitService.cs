namespace KasseAPI_Final.Services.Countries.KassenSicherheit;

public sealed record KassenSicherheitSignRequest(
    Guid TenantId,
    string Payload,
    string? CashRegisterId = null);

public sealed record KassenSicherheitSignResult(
    bool Signed,
    string? Signature,
    string? Provider);

/// <summary>
/// DE TSE/KassenSicherheit facade. Shape and gating only — no vendor integration.
/// </summary>
public interface IKassenSicherheitService
{
    Task<KassenSicherheitSignResult> SignAsync(
        KassenSicherheitSignRequest request,
        CancellationToken cancellationToken = default);
}
