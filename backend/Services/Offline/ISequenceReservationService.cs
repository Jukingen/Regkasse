namespace KasseAPI_Final.Services.Offline;

/// <summary>
/// Reserves fiscal receipt sequence numbers (BelegNr counters) for offline order replay.
/// Batch-reserves upfront to avoid per-order allocation races during replay.
/// </summary>
public interface ISequenceReservationService
{
    /// <summary>
    /// Atomically reserves <paramref name="count"/> consecutive sequence counters for the register (UTC day).
    /// </summary>
    Task<List<int>> ReserveSequencesAsync(
        int count,
        Guid cashRegisterId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns unused tail reservations back to the pool (decrements <c>receipt_sequences.next_sequence</c>).
    /// </summary>
    Task ReleaseSequencesAsync(
        List<int> sequences,
        Guid cashRegisterId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true when the BelegNr for the sequence is not yet used in active payments.
    /// </summary>
    Task<bool> IsSequenceAvailableAsync(
        int sequenceNumber,
        Guid cashRegisterId,
        CancellationToken ct = default);

    /// <summary>
    /// Builds the human-readable BelegNr for a reserved counter.
    /// </summary>
    Task<string> ToBelegNrAsync(
        Guid cashRegisterId,
        int sequenceNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically reserves the next BelegNr for the register. Retries up to <paramref name="maxAttempts"/>
    /// when allocation fails or the number is already taken.
    /// </summary>
    Task<string> ReserveNextReceiptNumberAsync(
        Guid cashRegisterId,
        CancellationToken ct = default,
        int maxAttempts = 3);
}
