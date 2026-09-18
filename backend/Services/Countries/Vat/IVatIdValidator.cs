using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries.Strategies;

namespace KasseAPI_Final.Services.Countries.Vat;

/// <summary>
/// Shared VAT-ID engine. Shape checks read <see cref="CountryProfile.MatchesVatIdShape"/> — there is
/// no second regex motor. VIES is a separate, optional step gated by <c>Vies.CheckEnabled</c>.
/// </summary>
public interface IVatIdValidator
{
    /// <summary>
    /// Shape-only. Strict: no trimming and no case folding inside the check. Call sites that accept
    /// user-typed input call <see cref="Normalize"/> first.
    /// </summary>
    VatIdValidationResult Validate(string? vatId, CountryProfile profile);

    /// <summary>
    /// Trim + uppercase. Does <strong>not</strong> strip the country prefix — the stored value must
    /// still match the profile regex. VIES wire format splits country and number separately.
    /// </summary>
    string Normalize(string? vatId, CountryProfile profile);

    /// <summary>
    /// Optional VIES lookup. The client is not called when the flag is off or the profile is outside
    /// the EU VAT area (CH, EU_DEFAULT). EU coverage is every tenant-selectable profile that
    /// supports reverse charge — FR/IT join automatically when seeded.
    /// </summary>
    Task<ViesLookupResult> CheckViesAsync(
        string vatId,
        CountryProfile profile,
        CancellationToken cancellationToken = default);
}
