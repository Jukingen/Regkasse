namespace KasseAPI_Final.Configuration;

/// <summary>
/// Stub for the planned DE KassenSicherheit module. Separate from Austrian <c>Tse:</c>.
/// Startup lock lives in <c>CountryFiscalLockEvaluator</c>; this type does not sign or talk to a TSE.
/// </summary>
public sealed class KassenSicherheitOptions
{
    public const string SectionName = "KassenSicherheit";

    /// <summary>Future provider id. Production stub uses <c>not-configured</c>; <c>fake</c> is rejected outside Development.</summary>
    public string Provider { get; set; } = "not-configured";

    /// <summary>Must stay false outside Development. Independent of <see cref="Provider"/>.</summary>
    public bool AllowSimulatedTse { get; set; }

    /// <summary>
    /// <c>Simulation</c> is a Development-only signer mode. Empty means the host validator does not treat it as simulation.
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    public const string ModeSimulation = "Simulation";

    /// <summary><c>TEST</c> for the SIGN DE sandbox. <c>LIVE</c> is rejected when <see cref="PilotMode"/> is true.</summary>
    public string Environment { get; set; } = string.Empty;

    public const string EnvironmentTest = "TEST";

    public const string EnvironmentLive = "LIVE";

    /// <summary>
    /// When true, startup accepts only <see cref="EnvironmentTest"/> and the SIGN DE middleware TEST host.
    /// Default false: no DE signing from the country profile (<c>Fiscal.KassenSicherheitDe</c> stays off).
    /// A pilot still needs one tenant override; this flag does not turn the feature on.
    /// </summary>
    public bool PilotMode { get; set; }

    /// <summary>SIGN DE host. Not the Austrian <c>Fiskaly:ApiBaseUrl</c>.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    public const string SignDeTestHost = "kassensichv-middleware.fiskaly.com";

    public const string SignDeLiveHost = "kassensichv.fiskaly.com";

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    public string AdminPin { get; set; } = string.Empty;

    /// <summary>Per-request timeout in seconds. The client clamps this to 3–5.</summary>
    public int HttpTimeoutSeconds { get; set; } = 5;
}
