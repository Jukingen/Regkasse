using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// In-code OSS destination STANDARD rates. Read-only. Unknown, null, and blank codes return
/// null — never an Austrian fallback. Greek VAT-ID prefix <c>EL</c> resolves to seeded <c>GR</c>.
/// </summary>
public interface IOssVatRateRegistry
{
    /// <summary>
    /// STANDARD OSS rate after VAT-ID prefix alias normalization (<c>EL</c> → <c>GR</c>).
    /// Null when unknown, null, or blank. No AT fallback.
    /// </summary>
    decimal? GetStandardRate(string destinationCountryCode);

    IReadOnlyList<OssVatRate> GetAll();
}
