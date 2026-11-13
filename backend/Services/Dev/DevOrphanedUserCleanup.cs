using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Dev;

/// <summary>
/// Development-only helpers for removing test users tied to deleted demo tenants (dev, prod, test).
/// </summary>
internal static class DevOrphanedUserCleanup
{
    public static IQueryable<ApplicationUser> WhereOrphanedTestUserEmail(IQueryable<ApplicationUser> users) =>
        users.Where(u =>
            u.Email != null && (
                u.Email.Contains("@prod.")
                || u.Email.Contains("@dev.")
                || u.Email.Contains("@test.")
                || u.Email == "prod@prod.com"
                || u.Email == "PROD@PROD.COM"));
}
