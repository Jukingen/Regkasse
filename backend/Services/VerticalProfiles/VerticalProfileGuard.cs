using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.VerticalProfiles;

public sealed class VerticalProfileGuard : IVerticalProfileGuard
{
    public const string TaxiFeature = "taxi";
    public const string MobileServicesFeature = "mobile-services";

    private readonly IVerticalProfileService _profiles;
    private readonly ILogger<VerticalProfileGuard> _logger;

    public VerticalProfileGuard(
        IVerticalProfileService profiles,
        ILogger<VerticalProfileGuard> logger)
    {
        _profiles = profiles;
        _logger = logger;
    }

    public async Task<bool> IsFeatureEnabledAsync(string feature, CancellationToken cancellationToken)
    {
        var profile = await CurrentAsync(cancellationToken).ConfigureAwait(false);
        return profile?.HasPosFeature(feature) == true;
    }

    public Task EnforceAsync(string feature, CancellationToken cancellationToken) =>
        EnforceFeatureAsync(feature, endpoint: false, cancellationToken);

    public Task EnforceEndpointAsync(string feature, CancellationToken cancellationToken) =>
        EnforceFeatureAsync(feature, endpoint: true, cancellationToken);

    public async Task<bool> IsTaxiSurfaceAsync(CancellationToken cancellationToken)
    {
        var profile = await CurrentAsync(cancellationToken).ConfigureAwait(false);
        return IsTaxiSurface(profile);
    }

    public async Task EnforceTaxiSurfaceAsync(CancellationToken cancellationToken)
    {
        if (await IsTaxiSurfaceAsync(cancellationToken).ConfigureAwait(false))
            return;

        Reject(TaxiFeature, endpoint: false);
    }

    public async Task EnforceMobileServicesAsync(CancellationToken cancellationToken)
    {
        var profile = await CurrentAsync(cancellationToken).ConfigureAwait(false);
        if (string.Equals(profile?.ProfileId, VerticalProfileIds.MobileServices, StringComparison.Ordinal))
            return;

        Reject(MobileServicesFeature, endpoint: false);
    }

    private async Task EnforceFeatureAsync(string feature, bool endpoint, CancellationToken cancellationToken)
    {
        if (await IsFeatureEnabledAsync(feature, cancellationToken).ConfigureAwait(false))
            return;

        Reject(feature, endpoint);
    }

    private async Task<EffectiveVerticalProfileDto?> CurrentAsync(CancellationToken cancellationToken) =>
        await _profiles.GetForCurrentTenantAsync(cancellationToken).ConfigureAwait(false);

    private void Reject(string feature, bool endpoint)
    {
        _logger.LogInformation(
            "PROFILE_FEATURE_REJECTED feature={Feature} endpoint={Endpoint}",
            feature,
            endpoint);

        if (endpoint)
            throw new ProfileEndpointDisabledException(feature);

        throw new FeatureNotEnabledForProfileException(feature);
    }

    internal static bool IsTaxiSurface(EffectiveVerticalProfileDto? profile)
    {
        if (profile is null)
            return false;

        return profile.IsTaxiProfile
            || string.Equals(profile.PosLayout, VerticalProfileLayouts.Taxi, StringComparison.Ordinal);
    }
}
