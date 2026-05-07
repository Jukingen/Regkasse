using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>
/// Thread-safe holder for the latest NTP measurement used by fiscal guards and GET /api/system/time/status.
/// </summary>
public sealed class NtpTimeSyncStatus : INtpTimeSyncStatus
{
    private readonly object _gate = new();

    private DateTime _lastSyncAtUtc;
    private DateTime _systemTimeUtcAtSync;
    private DateTime? _ntpTimeUtcAtSync;
    private double? _offsetSeconds;
    private bool _lastAttemptSuccess;
    private string? _lastErrorMessage;

    public void RecordSynchronizationAttempt(
        DateTime syncTimeUtc,
        DateTime systemTimeUtc,
        DateTime? ntpTimeUtc,
        double? offsetSeconds,
        bool success,
        string? errorMessage)
    {
        lock (_gate)
        {
            _lastSyncAtUtc = syncTimeUtc;
            _systemTimeUtcAtSync = systemTimeUtc;
            _ntpTimeUtcAtSync = ntpTimeUtc;
            _offsetSeconds = offsetSeconds;
            _lastAttemptSuccess = success;
            _lastErrorMessage = errorMessage;
        }
    }

    public SystemTimeStatusDto BuildStatusDto(NtpSettings settings)
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            double? offsetLive = _offsetSeconds;
            DateTime? ntpNow = offsetLive.HasValue ? now.AddSeconds(offsetLive.Value) : null;

            var synchronized = _lastAttemptSuccess
                               && offsetLive.HasValue
                               && Math.Abs(offsetLive.Value) <= settings.MaxAllowedOffsetSeconds;

            var level = "ok";
            if (!_lastAttemptSuccess)
                level = "critical";
            else if (offsetLive.HasValue && Math.Abs(offsetLive.Value) > settings.CriticalOffsetSeconds)
                level = "critical";
            else if (!synchronized)
                level = "warning";

            return new SystemTimeStatusDto
            {
                SystemTimeUtc = now,
                NtpTimeUtc = ntpNow ?? _ntpTimeUtcAtSync,
                OffsetSeconds = offsetLive,
                IsSynchronized = synchronized,
                LastSyncAt = _lastSyncAtUtc,
                WarningLevel = level
            };
        }
    }

    public bool ShouldAllowOnlineFiscalPayment(NtpSettings settings, out string? operatorMessageDe)
    {
        operatorMessageDe = null;
        if (!settings.Enabled)
            return true;

        lock (_gate)
        {
            if (!_lastAttemptSuccess)
            {
                operatorMessageDe =
                    "Systemzeit nicht synchronisiert – bitte Administrator kontaktieren";
                return false;
            }

            if (!_offsetSeconds.HasValue)
            {
                operatorMessageDe =
                    "Systemzeit nicht synchronisiert – bitte Administrator kontaktieren";
                return false;
            }

            if (Math.Abs(_offsetSeconds.Value) > settings.MaxAllowedOffsetSeconds)
            {
                operatorMessageDe =
                    "Systemzeit nicht synchronisiert – bitte Administrator kontaktieren";
                return false;
            }

            return true;
        }
    }
}
