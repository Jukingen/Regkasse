using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

public interface IRestoreVerificationManualTriggerService
{
    /// <param name="idempotencyKey">İsteğe bağlı; aynı anahtar aynı satırı döndürür (terminal dahil).</param>
    Task<RestoreVerificationManualTriggerResult> EnqueueManualAsync(
        string? requestedByUserId,
        string? correlationId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}
