namespace KasseAPI_Final.Configuration;

/// <summary>
/// Stub for the planned CH MWST module. Startup lock lives in <c>CountryFiscalLockEvaluator</c>.
/// </summary>
public sealed class MwstOptions
{
    public const string SectionName = "Mwst";

    /// <summary>Must stay false in Production/Staging when the key is set.</summary>
    public bool UseTestEndpoint { get; set; }
}
