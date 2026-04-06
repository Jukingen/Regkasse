using System.Collections.Generic;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>Optional tenant-scoped probe of <c>company_settings</c> FinanzOnline columns (admin readiness only; never exposes secrets).</summary>
public sealed class FinanzOnlineReadinessTenantCompanyProbe
{
    /// <summary>True when the evaluator should apply tenant DB checks (admin endpoint with resolved tenant).</summary>
    public bool WasEvaluated { get; init; }

    public bool CompanySettingsRowExists { get; init; }

    public bool HasFinanzOnlineApiUrl { get; init; }

    public bool HasFinanzOnlineUsername { get; init; }

    public bool HasFinanzOnlinePassword { get; init; }

    public bool HasFinanzOnlineTelematikId { get; init; }

    public bool HasFinanzOnlineHerstellerId { get; init; }
}

/// <summary>Stable boolean surface for admin UI and automation (no secret values).</summary>
public sealed class FinanzOnlineReadinessDiagnosticsDto
{
    public bool SessionLayerSimulated { get; init; }

    public bool RegistrierkassenLayerSimulated { get; init; }

    public bool TransmissionQueryLayerSimulated { get; init; }

    public bool MixedTransportLayers { get; init; }

    public bool AnyLayerSimulated { get; init; }

    public bool AllLayersReal { get; init; }

    /// <summary>True when <c>FinanzOnlineOutbox:Enabled</c> is true (background worker runs).</summary>
    public bool OutboxPipelineEnabled { get; init; }

    public bool RegistrierkassenEnableRealTestSubmission { get; init; }

    public bool TransmissionQueryEnableRealTestQuery { get; init; }

    public bool ConnectivityUsesCompanySettings { get; init; }

    /// <summary>Null when not evaluated (public health probe); true/false after admin tenant DB read.</summary>
    public bool? CompanySettingsProbeEvaluated { get; init; }

    /// <summary>Null when probe not evaluated; otherwise whether required FinanzOnline company fields are present.</summary>
    public bool? CompanySettingsFinanzOnlineComplete { get; init; }

    public bool ConfigSessionBaseUrlConfigured { get; init; }

    public bool ConfigRkdbBaseUrlConfigured { get; init; }

    /// <summary>When connectivity uses config (not company settings), default or scoped session credentials include username and password.</summary>
    public bool ConfigSessionHasUsernamePassword { get; init; }

    /// <summary>When connectivity uses config, default or scoped session credentials include TelematikId and HerstellerId.</summary>
    public bool ConfigSessionHasParticipantIds { get; init; }

    /// <summary>Effective session endpoint available for real transport (config BaseUrl or probed company API URL).</summary>
    public bool SessionEndpointResolvable { get; init; }

    /// <summary>Effective rkdb endpoint available for real transport (config BaseUrl or probed company API URL).</summary>
    public bool RkdbEndpointResolvable { get; init; }
}

/// <summary>API / health JSON contract for FinanzOnline configuration readiness (no secrets).</summary>
public sealed class FinanzOnlineReadinessResponse
{
    /// <summary>Healthy, Degraded, or Unhealthy.</summary>
    public string OverallStatus { get; init; } = "Degraded";

    /// <summary>AllSimulated, Mixed, or AllReal — layer alignment of Session / Registrierkassen / TransmissionQuery.</summary>
    public string TransportMode { get; init; } = "AllSimulated";

    /// <summary>True when config allows real SOAP rkdb TEST submission (not a live probe).</summary>
    public bool RealTestSubmissionPossible { get; init; }

    /// <summary>True when config allows real status_kasse / protocol reconciliation for TEST.</summary>
    public bool ProtocolReconciliationPossible { get; init; }

    public bool OutboxWorkerEnabled { get; init; }

    /// <summary>Human-readable English summary for operators and logs.</summary>
    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<FinanzOnlineReadinessFindingDto> Findings { get; init; } = [];

    /// <summary>Populated only on admin readiness endpoint (optional).</summary>
    public IReadOnlyDictionary<string, int>? OutboxCountsByStatus { get; init; }

    /// <summary>Trimmed <c>FinanzOnline:Simulation:Scenario</c> when not None (may be unknown string).</summary>
    public string? ConfiguredSimulationScenario { get; init; }

    /// <summary>Canonical scenario id when host guards allow it; otherwise null.</summary>
    public string? EffectiveSimulationScenario { get; init; }

    /// <summary>From <c>FinanzOnline:Simulation:FixedDelayMs</c> when &gt; 0.</summary>
    public int? SimulationFixedDelayMs { get; init; }

    /// <summary>From <c>FinanzOnline:Simulation:Seed</c> when non-zero.</summary>
    public long? SimulationSeed { get; init; }

    /// <summary>Structured flags derived from the same rules as <see cref="Findings"/> (UI and probes).</summary>
    public FinanzOnlineReadinessDiagnosticsDto? Diagnostics { get; init; }
}

public sealed class FinanzOnlineReadinessFindingDto
{
    public string Severity { get; init; } = "Warning";

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    /// <summary>Optional grouping for UIs: Transport, Simulation, Credentials, CompanySettings, Outbox, DevTest.</summary>
    public string? Category { get; init; }
}
