namespace KasseAPI_Final.Models;

/// <summary>
/// Demo is not a role in the final model; use IsDemo flag. This helper keeps a single predicate
/// for PaymentService/UserService until all DB rows are migrated off Role == Demo.
/// </summary>
public static class DemoUserHelper
{
    /// <summary>
    /// True when user must be treated as demo (restricted payments/refunds). Prefer IsDemo; Role == Demo only for backward compat.
    /// </summary>
    public static bool IsDemoUser(ApplicationUser? user)
    {
        if (user == null) return false;
        if (user.IsDemo) return true;
        return string.Equals(user.Role, "Demo", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Why the user is treated as demo; null if not demo. For structured logging / diagnostic responses only.
    /// </summary>
    public static string? GetDemoRejectionReason(ApplicationUser? user)
    {
        if (user == null) return null;
        if (!IsDemoUser(user)) return null;
        if (user.IsDemo) return "DEMO_BY_FLAG";
        if (string.Equals(user.Role, "Demo", StringComparison.OrdinalIgnoreCase)) return "DEMO_BY_ROLE";
        return "DEMO_UNKNOWN";
    }
}
