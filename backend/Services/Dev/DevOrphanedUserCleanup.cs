using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Dev;

/// <summary>
/// Development-only helpers for removing test users tied to deleted demo tenants (bar, cafe, test).
/// </summary>
internal static class DevOrphanedUserCleanup
{
    public static IQueryable<ApplicationUser> WhereOrphanedTestUserEmail(IQueryable<ApplicationUser> users) =>
        users.Where(u =>
            u.Email != null && (
                u.Email.Contains("@bar.")
                || u.Email.Contains("@cafe.")
                || u.Email.Contains("@test.")
                || u.Email == "bar@bar.com"
                || u.Email == "BAR@BAR.COM"));
}
