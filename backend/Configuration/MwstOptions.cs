namespace KasseAPI_Final.Configuration;

/// <summary>
/// Stub for the planned CH MWST module. Startup lock lives in <c>CountryFiscalLockEvaluator</c>.
/// </summary>
public sealed class MwstOptions
{
    public const string SectionName = "Mwst";

    /// <summary>Must stay false in Production/Staging when the key is set.</summary>
    public bool UseTestEndpoint { get; set; }

    /// <summary>
    /// Single CH mandant allowed to run live MWST + QR. Empty denies every tenant.
    /// Env: <c>Mwst__CanaryTenantId</c>. KassenSicherheit is not used for CH.
    /// </summary>
    public string CanaryTenantId { get; set; } = string.Empty;
}
