using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>
/// In-memory snapshot of the last NTP synchronization (updated by <see cref="NtpTimeSyncService"/>).
/// </summary>
public interface INtpTimeSyncStatus
{
    void RecordSynchronizationAttempt(
        DateTime syncTimeUtc,
        DateTime systemTimeUtc,
        DateTime? ntpTimeUtc,
        double? offsetSeconds,
        bool success,
        string? errorMessage);

    SystemTimeStatusDto BuildStatusDto(NtpSettings settings);

    /// <summary>
    /// When false, online fiscal payments must be rejected (RKSV / FinanzOnline DEP guard).
    /// </summary>
    bool ShouldAllowOnlineFiscalPayment(NtpSettings settings, out string? operatorMessageDe);
}
