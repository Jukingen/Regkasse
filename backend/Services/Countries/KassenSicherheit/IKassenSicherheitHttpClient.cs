namespace KasseAPI_Final.Services.Countries.KassenSicherheit;

/// <summary>
/// fiskaly SIGN DE (cloud TSS) HTTP API. Separate from Austrian SIGN AT (<c>IFiskalyClient</c>).
/// </summary>
public interface IKassenSicherheitHttpClient
{
    Task<KassenSicherheitAuthResult> AuthenticateAsync(CancellationToken cancellationToken = default);

    Task<KassenSicherheitTssResult> CreateAndInitializeTssAsync(
        KassenSicherheitCreateTssRequest request,
        CancellationToken cancellationToken = default);

    Task<KassenSicherheitClientResult> CreateClientAsync(
        KassenSicherheitCreateClientRequest request,
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
