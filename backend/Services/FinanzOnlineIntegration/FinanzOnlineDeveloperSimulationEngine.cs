using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// In-process state for optional FinanzOnline simulation scenarios (simulated transport only).
/// Precedence: <see cref="FinanzOnlineSimulationOptions.Scenario"/> when effective; otherwise legacy <c>FinanzOnline:Simulation:Developer:BehaviorProfile</c>.
/// </summary>
public sealed class FinanzOnlineDeveloperSimulationEngine
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptionsMonitor<FinanzOnlineSimulationDeveloperOptions> _developerOptions;
    private readonly IOptionsMonitor<FinanzOnlineSimulationOptions> _simulationOptions;
    private readonly ILogger<FinanzOnlineDeveloperSimulationEngine> _logger;
    private readonly ConcurrentDictionary<string, int> _submitAttempts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _protocolQueries = new(StringComparer.Ordinal);

    public FinanzOnlineDeveloperSimulationEngine(
        IHostEnvironment hostEnvironment,
        IOptionsMonitor<FinanzOnlineSimulationDeveloperOptions> developerOptions,
        IOptionsMonitor<FinanzOnlineSimulationOptions> simulationOptions,
        ILogger<FinanzOnlineDeveloperSimulationEngine> logger)
    {
        _hostEnvironment = hostEnvironment;
        _developerOptions = developerOptions;
        _simulationOptions = simulationOptions;
        _logger = logger;
    }

    /// <summary>True when config scenario or legacy developer profile is active (non-Production only).</summary>
    public bool IsNonNoneProfileActive()
    {
        if (_hostEnvironment.IsProduction())
            return false;
        if (!string.IsNullOrEmpty(GetEffectiveCanonicalScenarioName(_hostEnvironment, _simulationOptions.CurrentValue)))
            return true;
        if (!ShouldApplyDeveloperLegacyProfiles())
            return false;
        var profile = (_developerOptions.CurrentValue.BehaviorProfile ?? "None").Trim();
        return profile.Length > 0 && !string.Equals(profile, "None", StringComparison.OrdinalIgnoreCase);
    }

    public string? ActiveProfileOrNull()
    {
        var s = GetEffectiveCanonicalScenarioName(_hostEnvironment, _simulationOptions.CurrentValue);
        if (!string.IsNullOrEmpty(s))
            return s;
        if (!ShouldApplyDeveloperLegacyProfiles())
            return null;
        var profile = (_developerOptions.CurrentValue.BehaviorProfile ?? "None").Trim();
        if (profile.Length == 0 || string.Equals(profile, "None", StringComparison.OrdinalIgnoreCase))
            return null;
        return profile;
    }

    /// <summary>Scenario path: Development or explicit non-production flag; never Production.</summary>
    public static bool ShouldApplySimulationScenario(IHostEnvironment env, FinanzOnlineSimulationOptions sim) =>
        !env.IsProduction() && (env.IsDevelopment() || sim.EnableScenarioOutsideDevelopment);

    /// <summary>Returns canonical scenario id when configuration applies and <see cref="FinanzOnlineSimulationOptions.Scenario"/> is recognized.</summary>
    public static string? GetEffectiveCanonicalScenarioName(IHostEnvironment env, FinanzOnlineSimulationOptions sim)
    {
        if (!ShouldApplySimulationScenario(env, sim))
            return null;
        return NormalizeScenarioName(sim.Scenario);
    }

    /// <summary>Raw configured scenario (trimmed), or null when None/empty.</summary>
    public static string? GetConfiguredScenarioRawOrNull(FinanzOnlineSimulationOptions sim)
    {
        var t = (sim.Scenario ?? "").Trim();
        if (t.Length == 0 || string.Equals(t, FinanzOnlineSimulationScenarios.None, StringComparison.OrdinalIgnoreCase))
            return null;
        return t;
    }

    private static string? NormalizeScenarioName(string? raw)
    {
        var t = (raw ?? "").Trim();
        if (t.Length == 0 || string.Equals(t, FinanzOnlineSimulationScenarios.None, StringComparison.OrdinalIgnoreCase))
            return null;
        if (string.Equals(t, FinanzOnlineSimulationScenarios.ImmediateSuccess, StringComparison.OrdinalIgnoreCase))
            return FinanzOnlineSimulationScenarios.ImmediateSuccess;
        if (string.Equals(t, FinanzOnlineSimulationScenarios.RetryThenSuccess, StringComparison.OrdinalIgnoreCase))
            return FinanzOnlineSimulationScenarios.RetryThenSuccess;
        if (string.Equals(t, FinanzOnlineSimulationScenarios.PermanentFailure, StringComparison.OrdinalIgnoreCase))
            return FinanzOnlineSimulationScenarios.PermanentFailure;
        if (string.Equals(t, FinanzOnlineSimulationScenarios.AwaitingProtocolThenSuccess, StringComparison.OrdinalIgnoreCase))
            return FinanzOnlineSimulationScenarios.AwaitingProtocolThenSuccess;
        if (string.Equals(t, FinanzOnlineSimulationScenarios.DeadLetter, StringComparison.OrdinalIgnoreCase))
            return FinanzOnlineSimulationScenarios.DeadLetter;
        return null;
    }

    private bool ShouldApplyDeveloperLegacyProfiles() =>
        !_hostEnvironment.IsProduction()
        && (_hostEnvironment.IsDevelopment() || _developerOptions.CurrentValue.EnableBehaviorProfilesOutsideDevelopment);

    private string SeedCorrelationKey(string baseKey)
    {
        var seed = _simulationOptions.CurrentValue.Seed;
        if (seed == 0)
            return baseKey;
        return $"{baseKey}\0seed={seed}";
    }

    private async Task ApplyLatencyAsync(CancellationToken cancellationToken)
    {
        if (!IsNonNoneProfileActive())
            return;
        var simMs = Math.Max(0, _simulationOptions.CurrentValue.FixedDelayMs);
        var devMs = Math.Max(0, _developerOptions.CurrentValue.ArtificialLatencyMs);
        var ms = simMs > 0 ? simMs : devMs;
        if (ms <= 0)
            return;
        await Task.Delay(ms, cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyLatencyIfDevScenarioActiveAsync(CancellationToken cancellationToken = default) =>
        await ApplyLatencyAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Returns null to use stock simulated registrierkassen behavior; otherwise a fixed response for the scenario.
    /// </summary>
    public async Task<FinanzOnlineRegisterSubmissionResponse?> TryRegistrierkassenSubmitAsync(
        FinanzOnlineRegisterSubmissionRequest request,
        Func<Task<FinanzOnlineRegisterSubmissionResponse>> buildDefaultSuccessAsync,
        CancellationToken cancellationToken = default)
    {
        if (_hostEnvironment.IsProduction())
            return null;

        var simOpt = _simulationOptions.CurrentValue;
        var canon = GetEffectiveCanonicalScenarioName(_hostEnvironment, simOpt);
        if (canon != null)
        {
            await ApplyLatencyAsync(cancellationToken).ConfigureAwait(false);
            return await TrySubmitByCanonicalScenarioAsync(canon, simOpt, request, buildDefaultSuccessAsync, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!ShouldApplyDeveloperLegacyProfiles())
            return null;

        var profile = (_developerOptions.CurrentValue.BehaviorProfile ?? "None").Trim();
        if (string.Equals(profile, "None", StringComparison.OrdinalIgnoreCase))
            return null;

        await ApplyLatencyAsync(cancellationToken).ConfigureAwait(false);

        if (string.Equals(profile, "AlwaysSuccess", StringComparison.OrdinalIgnoreCase))
            return null;

        if (string.Equals(profile, "ImmediateProtocolSuccess", StringComparison.OrdinalIgnoreCase))
            return BuildImmediateProtocolSuccess(request);

        if (string.Equals(profile, "PermanentSubmitFailure", StringComparison.OrdinalIgnoreCase))
            return BuildPermanentFailure();

        if (string.Equals(profile, "RetryableUntilDeadLetter", StringComparison.OrdinalIgnoreCase))
            return BuildRetryableTransient();

        if (string.Equals(profile, "RetryableSubmitThenSuccess", StringComparison.OrdinalIgnoreCase))
        {
            return await TryRetryThenSuccessAsync(
                request,
                buildDefaultSuccessAsync,
                Math.Max(0, _developerOptions.CurrentValue.RetryableSubmitFailuresBeforeSuccess),
                cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(profile, "ProtocolPendingThenSuccess", StringComparison.OrdinalIgnoreCase))
            return null;

        _logger.LogWarning("Unknown FinanzOnline:Simulation:Developer BehaviorProfile={Profile}", profile);
        return null;
    }

    private async Task<FinanzOnlineRegisterSubmissionResponse?> TrySubmitByCanonicalScenarioAsync(
        string canon,
        FinanzOnlineSimulationOptions simOpt,
        FinanzOnlineRegisterSubmissionRequest request,
        Func<Task<FinanzOnlineRegisterSubmissionResponse>> buildDefaultSuccessAsync,
        CancellationToken cancellationToken)
    {
        switch (canon)
        {
            case FinanzOnlineSimulationScenarios.ImmediateSuccess:
                _logger.LogDebug(
                    "FinanzOnline simulation scenario ImmediateSuccess CorrelationId={CorrelationId}",
                    request.Correlation.CorrelationId);
                return BuildImmediateProtocolSuccess(request);
            case FinanzOnlineSimulationScenarios.PermanentFailure:
                _logger.LogDebug(
                    "FinanzOnline simulation scenario PermanentFailure CorrelationId={CorrelationId}",
                    request.Correlation.CorrelationId);
                return BuildPermanentFailure();
            case FinanzOnlineSimulationScenarios.DeadLetter:
                _logger.LogDebug(
                    "FinanzOnline simulation scenario DeadLetter CorrelationId={CorrelationId}",
                    request.Correlation.CorrelationId);
                return BuildRetryableTransient();
            case FinanzOnlineSimulationScenarios.RetryThenSuccess:
                return await TryRetryThenSuccessAsync(
                    request,
                    buildDefaultSuccessAsync,
                    Math.Max(0, simOpt.RetryCountBeforeSuccess),
                    cancellationToken).ConfigureAwait(false);
            case FinanzOnlineSimulationScenarios.AwaitingProtocolThenSuccess:
                return null;
            default:
                return null;
        }
    }

    private FinanzOnlineRegisterSubmissionResponse BuildImmediateProtocolSuccess(FinanzOnlineRegisterSubmissionRequest request)
    {
        var now = DateTime.UtcNow;
        var referenceId = $"SIM-IMM-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        return new FinanzOnlineRegisterSubmissionResponse
        {
            Success = true,
            TransmissionId = null,
            ReferenceId = referenceId,
            Status = "completed",
            ProtocolCode = "SIM_IMMEDIATE_OK",
            ProtocolSummary = "Simulated immediate rkdb acceptance (no status_kasse roundtrip).",
            RkdbTsErstellungIso = now.ToString("O")
        };
    }

    private static FinanzOnlineRegisterSubmissionResponse BuildPermanentFailure() =>
        new()
        {
            Success = false,
            ErrorCode = "RKDB_COMMAND_INVALID",
            ErrorMessage = "Simulation scenario: permanent submit failure."
        };

    private static FinanzOnlineRegisterSubmissionResponse BuildRetryableTransient() =>
        new()
        {
            Success = false,
            ErrorCode = "HTTP_503",
            ErrorMessage = "Simulation scenario: transient submit failure (outbox may reach DeadLetter after MaxAttempts)."
        };

    private async Task<FinanzOnlineRegisterSubmissionResponse?> TryRetryThenSuccessAsync(
        FinanzOnlineRegisterSubmissionRequest request,
        Func<Task<FinanzOnlineRegisterSubmissionResponse>> buildDefaultSuccessAsync,
        int failuresBeforeSuccess,
        CancellationToken cancellationToken)
    {
        var key = SeedCorrelationKey(request.Correlation.CorrelationId ?? request.Correlation.BusinessKey ?? "");
        var n = _submitAttempts.AddOrUpdate(key, 1, static (_, c) => c + 1);
        if (n <= failuresBeforeSuccess)
        {
            _logger.LogDebug(
                "FinanzOnline simulation: transient submit {Attempt}/{Max} CorrelationId={CorrelationId}",
                n,
                failuresBeforeSuccess,
                key);
            return new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = "HTTP_503",
                ErrorMessage = "Simulation: transient submit failure before success."
            };
        }

        _logger.LogDebug(
            "FinanzOnline simulation: submit success after {Failures} transient failures CorrelationId={CorrelationId}",
            failuresBeforeSuccess,
            key);
        return await buildDefaultSuccessAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Returns null to use stock simulated protocol response.
    /// </summary>
    public async Task<FinanzOnlineTransmissionStatusQueryResponse?> TryTransmissionQueryAsync(
        FinanzOnlineTransmissionStatusQueryRequest request,
        Func<Task<FinanzOnlineTransmissionStatusQueryResponse>> buildDefaultAsync,
        CancellationToken cancellationToken = default)
    {
        if (_hostEnvironment.IsProduction())
            return null;

        var simOpt = _simulationOptions.CurrentValue;
        var canon = GetEffectiveCanonicalScenarioName(_hostEnvironment, simOpt);
        var awaitingProtocol = canon == FinanzOnlineSimulationScenarios.AwaitingProtocolThenSuccess;

        if (!awaitingProtocol)
        {
            if (!ShouldApplyDeveloperLegacyProfiles())
                return null;
            var profile = (_developerOptions.CurrentValue.BehaviorProfile ?? "None").Trim();
            if (!string.Equals(profile, "ProtocolPendingThenSuccess", StringComparison.OrdinalIgnoreCase))
                return null;
        }

        await ApplyLatencyAsync(cancellationToken).ConfigureAwait(false);

        var pendingCount = awaitingProtocol
            ? Math.Max(0, simOpt.ProtocolPendingQueriesBeforeSuccess)
            : Math.Max(0, _developerOptions.CurrentValue.ProtocolPendingQueriesBeforeSuccess);
        var key = SeedCorrelationKey(request.TransmissionId ?? "");
        var n = _protocolQueries.AddOrUpdate(key, 1, static (_, c) => c + 1);
        if (n <= pendingCount)
        {
            _logger.LogDebug(
                "FinanzOnline simulation: protocol pending {Attempt}/{Max} TransmissionId={TransmissionId}",
                n,
                pendingCount,
                request.TransmissionId);
            return new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = true,
                TransmissionId = request.TransmissionId,
                Status = "pending",
                Protocol = new[]
                {
                    new FinanzOnlineTransmissionProtocolEntry
                    {
                        TimestampUtc = DateTimeOffset.UtcNow,
                        Level = "Info",
                        Message = "Simulation: awaiting protocol."
                    }
                }
            };
        }

        return await buildDefaultAsync().ConfigureAwait(false);
    }
}
