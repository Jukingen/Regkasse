namespace KasseAPI_Final.Services.Extensions;

/// <summary>
/// An enabled extension returned <see cref="ExtensionResult.Success"/> false.
/// Callers map this to HTTP 400 with <see cref="ErrorCode"/>.
/// </summary>
public sealed class ExtensionFailedException : Exception
{
    public const string ErrorCode = "EXTENSION_FAILED";

    public ExtensionFailedException(string featureName, string? message)
        : base(string.IsNullOrWhiteSpace(message)
            ? $"Extension '{featureName}' failed."
            : message)
    {
        FeatureName = featureName;
    }

    public string FeatureName { get; }
}
