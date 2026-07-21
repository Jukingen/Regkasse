using System.Diagnostics;
using System.Globalization;
using KasseAPI_Final.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Türkçe: Legacy <c>GET /api/FinanzOnline/status</c> ve <c>POST …/test-connection</c> için
/// simülasyon bayraklarını açığa çıkarır; gerçek taşımada yalnızca FinanzOnline SOAP oturum probu kullanılır (hafif).
/// </summary>
public interface IFinanzOnlineAdminConnectivityService
{
    Task<FinanzOnlineAdministrativeStatusSnapshot> BuildStatusAsync(TseDevice device, CancellationToken cancellationToken);

    Task<FinanzOnlineAdministrativeTestSnapshot> RunTestConnectionAsync(TseDevice device, CancellationToken cancellationToken);
}

/// <summary>Türkçe: Controller DTO eşlemesi için ara anlık görüntü.</summary>
public sealed class FinanzOnlineAdministrativeStatusSnapshot
{
    public bool IsConnected { get; init; }
    public string ApiVersion { get; init; } = string.Empty;
    public string LastSync { get; init; } = string.Empty;
    public int PendingInvoices { get; init; }
    public int PendingReports { get; init; }
    public string? ErrorMessage { get; init; }
    public bool FinanzOnlineTransportsSimulated { get; init; }
    public bool EnableRealTestSubmission { get; init; }
    public string TransportDiagnostics { get; init; } = string.Empty;
    public bool? SessionProbeSucceeded { get; init; }
    public string? SessionProbeTimestamp { get; init; }
    public string? SessionProbeIntegrationMode { get; init; }
    /// <summary>Türkçe: <c>IsConnected</c> değeri canlı BMF oturum probuna dayanıyorsa true (önbellekte geçerli prob var).</summary>
    public bool IsAuthoritative { get; init; }
}

/// <summary>Türkçe: Test-connection yanıtı için ara anlık görüntü.</summary>
public sealed class FinanzOnlineAdministrativeTestSnapshot
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string ApiVersion { get; init; } = string.Empty;
    public int ResponseTime { get; init; }
    public string Timestamp { get; init; } = string.Empty;
    public bool FinanzOnlineTransportsSimulated { get; init; }
    public bool EnableRealTestSubmission { get; init; }
    public string TransportDiagnostics { get; init; } = string.Empty;
    public string? ProbeIntegrationMode { get; init; }
    /// <summary>Türkçe: Yanıt gerçek SOAP oturum probuna dayanıyorsa true; simülasyon modunda false.</summary>
    public bool IsAuthoritative { get; init; }
}

public sealed class FinanzOnlineAdminConnectivityService : IFinanzOnlineAdminConnectivityService
{
    public const string SessionProbeCacheKey = "FinanzOnline:Admin:SessionProbe:v1";
    private static readonly TimeSpan ProbeCacheTtl = TimeSpan.FromMinutes(2);

    private readonly IFinanzOnlineSessionClient _sessionClient;
    private readonly IOptionsMonitor<FinanzOnlineSessionOptions> _sessionOptions;
    private readonly IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> _registrierkassenOptions;
    private readonly IOptionsMonitor<FinanzOnlineTransmissionQueryOptions> _transmissionOptions;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<FinanzOnlineAdminConnectivityService> _logger;

    public FinanzOnlineAdminConnectivityService(
        IFinanzOnlineSessionClient sessionClient,
        IOptionsMonitor<FinanzOnlineSessionOptions> sessionOptions,
        IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> registrierkassenOptions,
        IOptionsMonitor<FinanzOnlineTransmissionQueryOptions> transmissionOptions,
        IMemoryCache memoryCache,
        ILogger<FinanzOnlineAdminConnectivityService> logger)
    {
        _sessionClient = sessionClient;
        _sessionOptions = sessionOptions;
        _registrierkassenOptions = registrierkassenOptions;
        _transmissionOptions = transmissionOptions;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public Task<FinanzOnlineAdministrativeStatusSnapshot> BuildStatusAsync(TseDevice device, CancellationToken cancellationToken)
    {
        var sess = _sessionOptions.CurrentValue.UseSimulation;
        var reg = _registrierkassenOptions.CurrentValue.UseSimulation;
        var tx = _transmissionOptions.CurrentValue.UseSimulation;
        var realTest = _registrierkassenOptions.CurrentValue.EnableRealTestSubmission;
        var anySim = sess || reg || tx;
        var diag = BuildTransportDiagnosticsLine(sess, reg, tx, realTest);

        var lastSync = device.LastFinanzOnlineSync.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

        if (anySim)
        {
            return Task.FromResult(new FinanzOnlineAdministrativeStatusSnapshot
            {
                IsConnected = false,
                ApiVersion = "simulated",
                LastSync = lastSync,
                PendingInvoices = device.PendingInvoices,
                PendingReports = device.PendingReports,
                ErrorMessage =
                    "FinanzOnline integration runs with at least one simulated transport — outbound SOAP is not authoritative here.",
                FinanzOnlineTransportsSimulated = true,
                EnableRealTestSubmission = realTest,
                TransportDiagnostics = diag,
                SessionProbeSucceeded = null,
                SessionProbeTimestamp = null,
                SessionProbeIntegrationMode = null,
                IsAuthoritative = false
            });
        }

        _memoryCache.TryGetValue(SessionProbeCacheKey, out FinanzOnlineSessionProbeCacheEntry? cached);
        var probeOk = cached?.Success ?? false;
        var iso = cached?.ProbedAtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        var hasProbe = cached != null;

        return Task.FromResult(new FinanzOnlineAdministrativeStatusSnapshot
        {
            IsConnected = probeOk,
            ApiVersion = string.IsNullOrWhiteSpace(cached?.IntegrationModeLabel) ? "soap-session" : $"soap-session-{cached!.IntegrationModeLabel}",
            LastSync = lastSync,
            PendingInvoices = device.PendingInvoices,
            PendingReports = device.PendingReports,
            ErrorMessage = cached == null
                ? "No recent SOAP session probe — use POST /api/FinanzOnline/test-connection (or wait after running it)."
                : (probeOk ? null : cached.ErrorMessage),
            FinanzOnlineTransportsSimulated = false,
            EnableRealTestSubmission = realTest,
            TransportDiagnostics = diag,
            SessionProbeSucceeded = cached != null ? cached.Success : null,
            SessionProbeTimestamp = cached != null ? iso : null,
            SessionProbeIntegrationMode = cached?.IntegrationModeLabel,
            IsAuthoritative = hasProbe
        });
    }

    public async Task<FinanzOnlineAdministrativeTestSnapshot> RunTestConnectionAsync(TseDevice device, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var sess = _sessionOptions.CurrentValue.UseSimulation;
        var reg = _registrierkassenOptions.CurrentValue.UseSimulation;
        var tx = _transmissionOptions.CurrentValue.UseSimulation;
        var realTest = _registrierkassenOptions.CurrentValue.EnableRealTestSubmission;
        var anySim = sess || reg || tx;
        var diag = BuildTransportDiagnosticsLine(sess, reg, tx, realTest);

        if (anySim)
        {
            sw.Stop();
            return new FinanzOnlineAdministrativeTestSnapshot
            {
                Success = false,
                Message =
                    "At least one FinanzOnline transport is in simulation mode — no outbound SOAP session probe was executed.",
                ApiVersion = "simulated",
                ResponseTime = (int)sw.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                FinanzOnlineTransportsSimulated = true,
                EnableRealTestSubmission = realTest,
                TransportDiagnostics = diag,
                ProbeIntegrationMode = null,
                IsAuthoritative = false
            };
        }

        var (ok, modeLabel, errMsg) = await TrySoapSessionProbeAsync(cancellationToken).ConfigureAwait(false);
        sw.Stop();

        var entry = new FinanzOnlineSessionProbeCacheEntry
        {
            Success = ok,
            IntegrationModeLabel = modeLabel,
            ErrorMessage = errMsg,
            ProbedAtUtc = DateTimeOffset.UtcNow
        };

        _memoryCache.Set(
            SessionProbeCacheKey,
            entry,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ProbeCacheTtl });

        return new FinanzOnlineAdministrativeTestSnapshot
        {
            Success = ok,
            Message = ok
                ? $"SOAP FinanzOnline session OK (mode={modeLabel})."
                : (errMsg ?? "SOAP FinanzOnline session probe failed."),
            ApiVersion = "soap-session",
            ResponseTime = (int)sw.ElapsedMilliseconds,
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            FinanzOnlineTransportsSimulated = false,
            EnableRealTestSubmission = realTest,
            TransportDiagnostics = diag,
            ProbeIntegrationMode = modeLabel,
            IsAuthoritative = true
        };
    }

    private async Task<(bool Ok, string? ModeLabel, string? ErrorMessage)> TrySoapSessionProbeAsync(CancellationToken cancellationToken)
    {
        FinanzOnlineSessionAccessResult? last = null;
        foreach (var mode in new[] { FinanzOnlineIntegrationMode.TEST, FinanzOnlineIntegrationMode.PROD })
        {
            var correlationId = $"admin-probe-{Guid.NewGuid():N}";
            var request = new FinanzOnlineSessionLoginRequest
            {
                Mode = mode,
                Scope = new FinanzOnlineScope { RegisterId = string.Empty },
                Correlation = new FinanzOnlineCorrelationContext
                {
                    BusinessKey = "admin-connectivity-probe",
                    PayloadHash = "probe",
                    CorrelationId = correlationId
                }
            };

            try
            {
                last = await _sessionClient.GetValidSessionAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FinanzOnline admin SOAP session probe threw Mode={Mode}", mode);
                last = new FinanzOnlineSessionAccessResult
                {
                    Success = false,
                    ErrorCode = "PROBE_EXCEPTION",
                    ErrorMessage = ex.Message,
                    FailureKind = FinanzOnlineSessionFailureKind.Retryable
                };
            }

            if (last.Success)
            {
                var label = mode == FinanzOnlineIntegrationMode.TEST ? "TEST" : "PROD";
                _logger.LogInformation("FinanzOnline admin SOAP session probe succeeded Mode={Mode}", label);
                return (true, label, null);
            }
        }

        var code = last?.ErrorCode ?? "SESSION_PROBE_FAILED";
        var msg = last?.ErrorMessage ?? "Session probe failed for both TEST and PROD modes.";
        _logger.LogWarning("FinanzOnline admin SOAP session probe failed Code={Code} Message={Message}", code, msg);
        return (false, null, $"{code}: {msg}");
    }

    private static string BuildTransportDiagnosticsLine(bool sessionSim, bool regSim, bool txSim, bool enableRealTestSubmission)
    {
        return FormattableString.Invariant(
            $"Session.UseSimulation={sessionSim}; Registrierkassen.UseSimulation={regSim}; TransmissionQuery.UseSimulation={txSim}; Registrierkassen.EnableRealTestSubmission={enableRealTestSubmission}");
    }

    private sealed class FinanzOnlineSessionProbeCacheEntry
    {
        public bool Success { get; init; }
        public string? IntegrationModeLabel { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTimeOffset ProbedAtUtc { get; init; }
    }
}
