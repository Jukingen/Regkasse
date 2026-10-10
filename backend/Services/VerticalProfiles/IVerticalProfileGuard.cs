namespace KasseAPI_Final.Services.VerticalProfiles;

/// <summary>
/// Resolves the ambient tenant's effective vertical profile and rejects
/// profile-specific fields or endpoints when the capability is off.
/// </summary>
public interface IVerticalProfileGuard
{
    /// <summary>
    /// True when the ambient effective profile has <paramref name="feature"/> set to true.
    /// Missing tenant or profile is false.
    /// </summary>
    Task<bool> IsFeatureEnabledAsync(string feature, CancellationToken cancellationToken);

    /// <summary>
    /// Throws <see cref="FeatureNotEnabledForProfileException"/> (HTTP 400
    /// <c>PROFILE_FEATURE_DISABLED</c>) when <paramref name="feature"/> is off.
    /// </summary>
    Task EnforceAsync(string feature, CancellationToken cancellationToken);

    /// <summary>
    /// Throws <see cref="ProfileEndpointDisabledException"/> (HTTP 403
    /// <c>PROFILE_ENDPOINT_DISABLED</c>) when <paramref name="feature"/> is off.
    /// </summary>
    Task EnforceEndpointAsync(string feature, CancellationToken cancellationToken);

    /// <summary>True when the effective profile id is <c>taxi</c> or <c>posLayout</c> is <c>taxi</c>.</summary>
    Task<bool> IsTaxiSurfaceAsync(CancellationToken cancellationToken);

    /// <summary>Rejects taxi route fields when the tenant is not a taxi surface.</summary>
    Task EnforceTaxiSurfaceAsync(CancellationToken cancellationToken);

    /// <summary>Rejects mobile-service address fields when the profile id is not <c>mobile-services</c>.</summary>
    Task EnforceMobileServicesAsync(CancellationToken cancellationToken);
}
