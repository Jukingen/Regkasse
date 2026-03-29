using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Haftalık zamanlanmış restore drill planlaması için okuma sorguları (EF üzerinden).
/// </summary>
public interface IRestoreVerificationSchedulingQueryService
{
    /// <summary>
    /// Tetikleyici <see cref="RestoreVerificationTriggerSource.Scheduled"/>,
    /// durum <see cref="RestoreVerificationStatus.Succeeded"/>;
    /// kanıt zamanı <c>CompletedAt</c> (terminal başarı).
    /// </summary>
    Task<DateTime?> GetLastSuccessfulScheduledProofCompletedAtUtcAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Manuel veya zamanlanmış; yalnızca <c>ManualSuccessSatisfiesScheduledProofCadence</c> açıkken cadence için kullanılır.
    /// </summary>
    Task<DateTime?> GetLastSuccessfulAnyTriggerProofCompletedAtUtcAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Zamanlanmış satır Queued veya Running ise yeni zamanlanmış sıraya ekleme yapılmamalı (çift enqueue önleme).
    /// </summary>
    Task<bool> HasActiveScheduledQueuedOrRunningAsync(CancellationToken cancellationToken = default);
}
