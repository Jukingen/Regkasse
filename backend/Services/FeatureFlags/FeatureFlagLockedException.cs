namespace KasseAPI_Final.Services.FeatureFlags;

/// <summary>
/// Raised when an operator tries to disable a flag that the country profile locks on
/// (today: <c>Fiscal.RksvAt</c> for Austrian tenants).
/// </summary>
public sealed class FeatureFlagLockedException : InvalidOperationException
{
    public const string Code = "FEATURE_FLAG_LOCKED";

    public FeatureFlagLockedException(string flagName)
        : base($"Feature flag '{flagName}' is locked for this tenant and cannot be disabled.")
    {
        FlagName = flagName;
    }

    public string FlagName { get; }

    public string ErrorCode => Code;
}

/// <summary>
/// Raised when an operator tries to enable Austrian RKSV/TSE on a non-AT mandant.
/// </summary>
public sealed class FeatureFlagCountryRejectedException : InvalidOperationException
{
    public const string Code = "FEATURE_FLAG_COUNTRY_REJECTED";

    public FeatureFlagCountryRejectedException(string flagName, string countryCode)
        : base($"Feature flag '{flagName}' cannot be enabled for country '{countryCode}'.")
    {
        FlagName = flagName;
        CountryCode = countryCode;
    }

    public string FlagName { get; }

    public string CountryCode { get; }

    public string ErrorCode => Code;
}
