using KasseAPI_Final.Authorization;

namespace KasseAPI_Final.Services;

/// <summary>Device / platform label for admin session lists (POS Expo vs Admin web).</summary>
public static class SessionPlatformLabel
{
    public static string Resolve(string? clientApp, string? os)
    {
        if (string.Equals(clientApp, ClientAppPolicy.Pos, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(os, "Android", StringComparison.OrdinalIgnoreCase))
                return "POS (Android)";
            if (string.Equals(os, "iOS", StringComparison.OrdinalIgnoreCase))
                return "POS (iOS)";
            return "POS (Web)";
        }

        if (string.Equals(clientApp, ClientAppPolicy.Admin, StringComparison.OrdinalIgnoreCase))
            return "Admin";

        return string.IsNullOrWhiteSpace(clientApp) ? "Unknown" : clientApp.Trim();
    }
}
