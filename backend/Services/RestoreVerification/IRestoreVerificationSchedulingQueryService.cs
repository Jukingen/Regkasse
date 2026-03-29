using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Haftalık zamanlanmış restore drill planlaması için okuma sorguları (EF üzerinden).
/// </summary>
public interface IRestoreVerificationSchedulingQueryService
{
    /// <summary>
    /// Zamanlanmış sıra için cadence: tetikleyici <see cref="RestoreVerificationTriggerSource.Scheduled"/>,
    /// durum <see cref="RestoreVerificationStatus.Succeeded"/>, kanıt zamanı <c>CompletedAt</c> (terminal başarı; <c>RequestedAt</c> kullanılmaz).
    /// </summary>
    Task<DateTime?> GetLastSuccessfulScheduledProofCompletedAtUtcAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Zamanlanmış satır Queued veya Running ise yeni zamanlanmış sıraya ekleme yapılmamalı (çift enqueue önleme).
    /// </summary>
    Task<bool> HasActiveScheduledQueuedOrRunningAsync(CancellationToken cancellationToken = default);
}
