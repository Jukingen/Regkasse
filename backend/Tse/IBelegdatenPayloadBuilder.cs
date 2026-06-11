namespace KasseAPI_Final.Tse;

/// <summary>
/// Builds RKSV §9 <see cref="BelegdatenPayload"/> for a fiscal receipt row.
/// </summary>
public interface IBelegdatenPayloadBuilder
{
    Task<string?> TryGetCompactJwsAsync(
        Guid cashRegisterId,
        string receiptNumber,
        CancellationToken cancellationToken = default);

    Task<BelegdatenPayload> BuildAsync(
        Guid cashRegisterId,
        string receiptNumber,
        DateTime issuedAt,
        CancellationToken cancellationToken = default);
}
