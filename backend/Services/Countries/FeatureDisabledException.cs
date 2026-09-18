namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// Raised when a non-AT country module is invoked while its feature flag is off.
/// </summary>
public sealed class FeatureDisabledException : Exception
{
    public const string Code = "FEATURE_DISABLED";

    public FeatureDisabledException(string featureName)
        : base($"Feature '{featureName}' is disabled.")
    {
        FeatureName = featureName ?? throw new ArgumentNullException(nameof(featureName));
    }

    public string FeatureName { get; }

    public string ErrorCode => Code;
}
