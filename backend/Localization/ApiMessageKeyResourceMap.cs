namespace KasseAPI_Final.Localization;

/// <summary>Maps stable API message keys to <c>ErrorMessages.resx</c> entry names.</summary>
public static class ApiMessageKeyResourceMap
{
    private static readonly IReadOnlyDictionary<string, string> Map =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ApiMessageKeys.InvalidLoginCredentials] = "InvalidCredentials",
            [ApiMessageKeys.InvalidPassword] = "InvalidPassword",
            [ApiMessageKeys.UserNotFound] = "UserNotFound",
            [ApiMessageKeys.PasswordChangeRequired] = "PasswordChangeRequired",
            [ApiMessageKeys.LicenseExpired] = "LicenseExpired",
        };

    public static bool TryGetResourceName(string apiMessageKey, out string resourceName) =>
        Map.TryGetValue(apiMessageKey, out resourceName!);
}
