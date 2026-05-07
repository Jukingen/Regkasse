namespace KasseAPI_Final.Services;

/// <summary>
/// RKSV-aligned fiscal export disclaimer texts (not legally binding fiscal proof).
/// </summary>
public interface IDisclaimerService
{
    /// <summary>Returns the statutory-style disclaimer for DEP-like exports. <paramref name="language"/> is "de" or "en" (default German).</summary>
    string GetRksvDisclaimer(string language);
}
