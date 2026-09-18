using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.FeatureFlags;

namespace KasseAPI_Final.Services.Countries.Vat;

/// <summary>
/// Profile-driven VAT-ID engine. Shape uses the seed regex already compiled on
/// <see cref="CountryProfile"/>; VIES is opt-in and never runs in tests unless a mock is supplied.
/// </summary>
public sealed class VatIdValidator : IVatIdValidator
{
    private readonly IViesClient _viesClient;
    private readonly IFeatureFlagService? _featureFlags;

    public VatIdValidator(IViesClient viesClient, IFeatureFlagService? featureFlags = null)
    {
        _viesClient = viesClient ?? throw new ArgumentNullException(nameof(viesClient));
        _featureFlags = featureFlags;
    }

    public VatIdValidationResult Validate(string? vatId, CountryProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile.MatchesVatIdShape(vatId)
            ? VatIdValidationResult.Valid(vatId!)
            : VatIdValidationResult.Invalid();
    }

    public string Normalize(string? vatId, CountryProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return (vatId ?? string.Empty).Trim().ToUpperInvariant();
    }

    public async Task<ViesLookupResult> CheckViesAsync(
        string vatId,
        CountryProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!IsEuViesCountry(profile) || !IsViesEnabled())
            return ViesLookupResult.Valid();

        var (countryCode, vatNumber) = ToViesWireFormat(vatId, profile);
        return await _viesClient
            .LookupAsync(countryCode, vatNumber, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Tenant-selectable profiles that participate in the EU VAT area. CH is selectable but has no
    /// reverse-charge regime; EU_DEFAULT has the regime but is not selectable. Adding FR/IT with
    /// the same shape automatically includes them.
    /// </summary>
    internal static bool IsEuViesCountry(CountryProfile profile) =>
        profile.IsTenantSelectable && profile.Supports(VatRegime.EU_REVERSE_CHARGE);

    private bool IsViesEnabled() =>
        _featureFlags?.IsEnabled(FeatureFlagNames.ViesCheckEnabled) == true;

    /// <summary>
    /// VIES wants the ISO country code and the national number separately. The stored value keeps
    /// its prefix; only this wire split drops it.
    /// </summary>
    internal static (string CountryCode, string VatNumber) ToViesWireFormat(
        string vatId,
        CountryProfile profile)
    {
        var countryCode = profile.Code;
        if (vatId.StartsWith(countryCode, StringComparison.OrdinalIgnoreCase)
            && vatId.Length > countryCode.Length)
        {
            return (countryCode, vatId[countryCode.Length..]);
        }

        return (countryCode, vatId);
    }
}
