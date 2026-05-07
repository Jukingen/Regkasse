using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>
/// Test/DI fallback: never blocks fiscal flows (real status comes from <see cref="NtpTimeSyncStatus"/>).
/// </summary>
internal sealed class PermissiveNtpTimeSyncStatus : INtpTimeSyncStatus
{
    public static readonly PermissiveNtpTimeSyncStatus Instance = new();

    public void RecordSynchronizationAttempt(
        DateTime syncTimeUtc,
        DateTime systemTimeUtc,
        DateTime? ntpTimeUtc,
        double? offsetSeconds,
        bool success,
        string? errorMessage)
    {
    }

    public SystemTimeStatusDto BuildStatusDto(NtpSettings settings) =>
        new()
        {
            SystemTimeUtc = DateTime.UtcNow,
            NtpTimeUtc = DateTime.UtcNow,
            OffsetSeconds = 0,
            IsSynchronized = true,
            LastSyncAt = DateTime.UtcNow,
            WarningLevel = "ok"
        };

    public bool ShouldAllowOnlineFiscalPayment(NtpSettings settings, out string? operatorMessageDe)
    {
        operatorMessageDe = null;
        return true;
    }
}
