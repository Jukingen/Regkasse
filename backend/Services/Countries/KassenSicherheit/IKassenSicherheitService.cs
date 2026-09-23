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
/// DE TSE/KassenSicherheit facade. Austrian RKSV does not call this type.
/// </summary>
public interface IKassenSicherheitService
{
    /// <summary>
    /// AT-shaped sign call. DE fiskaly SIGN DE does not use this method;
    /// the <c>fiskaly-de</c> path uses <see cref="StartTransactionAsync"/> and
    /// <see cref="FinishTransactionAsync"/>.
    /// </summary>
    Task<KassenSicherheitSignResult> SignAsync(
        KassenSicherheitSignRequest request,
        CancellationToken cancellationToken = default);

    Task<KassenSicherheitTransactionResult> StartTransactionAsync(
        KassenSicherheitStartTransactionRequest request,
        CancellationToken cancellationToken = default);

    Task<KassenSicherheitTransactionResult> FinishTransactionAsync(
        KassenSicherheitFinishTransactionRequest request,
        CancellationToken cancellationToken = default);

    Task<KassenSicherheitExportResult> ExportDsfinvkAsync(
        KassenSicherheitExportRequest request,
        CancellationToken cancellationToken = default);
}
