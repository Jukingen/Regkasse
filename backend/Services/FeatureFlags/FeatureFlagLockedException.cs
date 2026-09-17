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
