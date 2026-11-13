namespace KasseAPI_Final.Services.VerticalProfiles;

/// <summary>Profile-specific request field used while the capability is off. HTTP 400.</summary>
public sealed class FeatureNotEnabledForProfileException : Exception
{
    public const string Code = "PROFILE_FEATURE_DISABLED";

    public const string DefaultMessage = "Feature not enabled for the current tenant profile.";

    public FeatureNotEnabledForProfileException(string feature)
        : base(DefaultMessage)
    {
        Feature = string.IsNullOrWhiteSpace(feature)
            ? throw new ArgumentException("Feature is required.", nameof(feature))
            : feature.Trim();
    }

    public string Feature { get; }

    public string ErrorCode => Code;
}

/// <summary>Profile-specific endpoint called for the wrong profile. HTTP 403.</summary>
public sealed class ProfileEndpointDisabledException : Exception
{
    public const string Code = "PROFILE_ENDPOINT_DISABLED";

    public const string DefaultMessage = "Endpoint is not enabled for the current tenant profile.";

    public ProfileEndpointDisabledException(string feature)
        : base(DefaultMessage)
    {
        Feature = string.IsNullOrWhiteSpace(feature)
            ? throw new ArgumentException("Feature is required.", nameof(feature))
            : feature.Trim();
    }

    public string Feature { get; }

    public string ErrorCode => Code;
}
