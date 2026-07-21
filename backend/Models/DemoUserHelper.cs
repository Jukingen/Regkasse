using KasseAPI_Final.Authorization;

namespace KasseAPI_Final.Models;

/// <summary>
/// Demo is not a role; use IsDemo flag. Determines demo treatment and which roles may hold the flag.
/// </summary>
public static class DemoUserHelper
{
    /// <summary>
    /// True when user must be treated as demo (restricted payments/refunds). Based only on IsDemo flag.
    /// </summary>
    public static bool IsDemoUser(ApplicationUser? user)
    {
        if (user == null)
            return false;
        return user.IsDemo;
    }

    /// <summary>
    /// Why the user is treated as demo; null if not demo. For structured logging / diagnostic responses only.
    /// </summary>
    public static string? GetDemoRejectionReason(ApplicationUser? user)
    {
        if (user == null)
            return null;
        if (!IsDemoUser(user))
            return null;
        return "DEMO_BY_FLAG";
    }

    /// <summary>
    /// Roles that may have IsDemo set. Used when auto-clearing IsDemo on user update (e.g. role changed to non-demo role).
    /// </summary>
    public static bool IsRoleAllowedForDemo(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;
        return string.Equals(role.Trim(), Roles.Cashier, StringComparison.OrdinalIgnoreCase);
    }
}
