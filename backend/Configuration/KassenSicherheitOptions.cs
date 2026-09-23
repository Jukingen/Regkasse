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

    /// <summary><c>TEST</c> for Development/Staging sandbox. <c>LIVE</c> is reserved for a later pilot.</summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>SIGN DE host. Not the Austrian <c>Fiskaly:ApiBaseUrl</c>.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    public string AdminPin { get; set; } = string.Empty;

    /// <summary>Per-request timeout in seconds. The client clamps this to 3–5.</summary>
    public int HttpTimeoutSeconds { get; set; } = 5;
}
