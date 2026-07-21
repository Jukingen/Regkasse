namespace KasseAPI_Final.Services.RestoreVerification;

public interface IRestoreVerificationManualTriggerService
{
    /// <summary>
    /// Manuel restore drill sıraya koyar veya mevcut satırı döndürür.
    /// Sıra: (1) dolu idempotency anahtarı ile eşleşen satır (her durum), (2) herhangi bir Queued/Running satırı,
    /// (3) yeni Queued satırı. PostgreSQL’de <c>idempotency_key</c> için kısmi benzersiz indeks yarışını yakalar.
    /// </summary>
    /// <param name="idempotencyKey">İsteğe bağlı; trim, en fazla 200 karakter; aynı değer aynı satırı döndürür (terminal dahil).</param>
    Task<RestoreVerificationManualTriggerResult> EnqueueManualAsync(
        string? requestedByUserId,
        string? correlationId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}
