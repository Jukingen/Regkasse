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
}
