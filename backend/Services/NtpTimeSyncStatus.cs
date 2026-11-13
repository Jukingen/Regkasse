using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Thread-safe holder for the latest NTP measurement used by fiscal guards and GET /api/system/time/status.
/// </summary>
public sealed class NtpTimeSyncStatus : INtpTimeSyncStatus
{
    private const string DevelopmentBypassLogMessage =
        "NTP check bypassed for development - do not use in production";

    private static int _developmentBypassLogged;
    private static int _developmentModeNtpBypassLogged;

    private readonly ILogger<NtpTimeSyncStatus> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptionsMonitor<DevelopmentOptions> _developmentOptions;
    private readonly IDevelopmentModeService _developmentModeService;
    private readonly object _gate = new();

    private DateTime _lastSyncAtUtc;
    private DateTime _systemTimeUtcAtSync;
    private DateTime? _ntpTimeUtcAtSync;
    private double? _offsetSeconds;
    private bool _lastAttemptSuccess;
    private string? _lastErrorMessage;

    public NtpTimeSyncStatus(
        ILogger<NtpTimeSyncStatus> logger,
        IHostEnvironment hostEnvironment,
        IOptionsMonitor<DevelopmentOptions> developmentOptions,
        IDevelopmentModeService developmentModeService)
    {
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _developmentOptions = developmentOptions;
        _developmentModeService = developmentModeService;
    }

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
            // Align operator UI with ShouldAllowOnlineFiscalPayment when NTP checks are off.
            if (!settings.Enabled)
            {
                return CreateSyntheticOkStatusDto();
            }

            if (_developmentModeService.ShouldBypassNtpCheck())
            {
                if (Interlocked.Exchange(ref _developmentModeNtpBypassLogged, 1) == 0)
                    _logger.LogWarning("Development mode active: {BypassType} bypassed", "NTP");
                return CreateSyntheticOkStatusDto();
            }

            if (IsActiveDevelopmentBypass(settings))
            {
                LogDevelopmentBypassOnce();
                return CreateSyntheticOkStatusDto();
            }

            if (SimulatingNtpFailureForFiscalGuard(settings))
            {
                var nowSim = DateTime.UtcNow;
                return new SystemTimeStatusDto
                {
                    SystemTimeUtc = nowSim,
                    NtpTimeUtc = nowSim.AddMinutes(-10),
                    OffsetSeconds = 999,
                    IsSynchronized = false,
                    LastSyncAt = nowSim,
                    WarningLevel = "critical"
                };
            }

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
        {
            _logger.LogWarning(
                "NTP fiscal guard disabled (NtpSettings.Enabled=false); online fiscal payments are not blocked by clock sync — not recommended for production.");
            return true;
        }

        if (_developmentModeService.ShouldBypassNtpCheck())
        {
            if (Interlocked.Exchange(ref _developmentModeNtpBypassLogged, 1) == 0)
                _logger.LogWarning("Development mode active: {BypassType} bypassed", "NTP");
            return true;
        }

        if (IsActiveDevelopmentBypass(settings))
        {
            LogDevelopmentBypassOnce();
            return true;
        }

        if (SimulatingNtpFailureForFiscalGuard(settings))
        {
            operatorMessageDe = "Systemzeit nicht synchronisiert – bitte Administrator kontaktieren";
            return false;
        }

        lock (_gate)
        {
            if (!_lastAttemptSuccess)
            {
                _logger.LogError("NTP sync failed — blocking fiscal payments.");
                operatorMessageDe =
                    "Systemzeit nicht synchronisiert – bitte Administrator kontaktieren";
                return false;
            }

            if (!_offsetSeconds.HasValue)
            {
                _logger.LogError("NTP sync has no measured offset — blocking fiscal payments.");
                operatorMessageDe =
                    "Systemzeit nicht synchronisiert – bitte Administrator kontaktieren";
                return false;
            }

            if (Math.Abs(_offsetSeconds.Value) > settings.MaxAllowedOffsetSeconds)
            {
                _logger.LogError(
                    "Time offset {OffsetSeconds}s exceeds MaxAllowedOffsetSeconds={MaxAllowedSeconds}s — blocking fiscal payments.",
                    _offsetSeconds.Value,
                    settings.MaxAllowedOffsetSeconds);
                operatorMessageDe =
                    "Systemzeit nicht synchronisiert – bitte Administrator kontaktieren";
                return false;
            }

            return true;
        }
    }

    private bool IsActiveDevelopmentBypass(NtpSettings settings) =>
        settings.DevelopmentBypass && _hostEnvironment.IsDevelopment();

    private bool SimulatingNtpFailureForFiscalGuard(NtpSettings settings) =>
        !OpenApiExportMode.IsEnabled
        && _hostEnvironment.IsDevelopment()
        && _developmentOptions.CurrentValue.SimulateNtpFailure
        && settings.Enabled
        && !_developmentModeService.ShouldBypassNtpCheck();

    private void LogDevelopmentBypassOnce()
    {
        if (Interlocked.Exchange(ref _developmentBypassLogged, 1) == 0)
            _logger.LogWarning(DevelopmentBypassLogMessage);
    }

    private static SystemTimeStatusDto CreateSyntheticOkStatusDto()
    {
        var now = DateTime.UtcNow;
        return new SystemTimeStatusDto
        {
            SystemTimeUtc = now,
            NtpTimeUtc = now,
            OffsetSeconds = 0,
            IsSynchronized = true,
            LastSyncAt = null,
            WarningLevel = "ok"
        };
    }
}
