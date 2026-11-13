using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Helpers;

/// <summary>
/// Case-insensitive login and username uniqueness using <see cref="ApplicationUser.NormalizedUserName"/>.
/// </summary>
public static class IdentityLoginLookup
{
    private static string NormalizeUserName(UserManager<ApplicationUser> userManager, string userName) =>
        userManager.NormalizeName(userName.Trim());

    /// <summary>Resolve user by email (normalized) or username (case-insensitive).</summary>
    public static async Task<ApplicationUser?> FindByLoginIdentifierAsync(
        UserManager<ApplicationUser> userManager,
        string loginIdentifier,
        CancellationToken cancellationToken = default)
    {
        var trimmed = loginIdentifier.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        var byEmail = await userManager.FindByEmailAsync(trimmed).ConfigureAwait(false);
        if (byEmail != null)
            return byEmail;

        return await FindByUserNameIgnoreCaseAsync(userManager, trimmed, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<ApplicationUser?> FindByUserNameIgnoreCaseAsync(
        UserManager<ApplicationUser> userManager,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var trimmed = userName.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        var normalized = NormalizeUserName(userManager, trimmed);
        var user = await userManager.Users
            .Where(u => u.NormalizedUserName == normalized)
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (user != null)
            return user;

        // Legacy rows without NormalizedUserName backfill
        var upper = trimmed.ToUpperInvariant();
        return await userManager.Users
            .Where(u =>
                u.NormalizedUserName == null
                && u.UserName != null
                && u.UserName.ToUpper() == upper)
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<bool> IsUserNameTakenByOtherUserAsync(
        UserManager<ApplicationUser> userManager,
        string userName,
        string? excludeUserId,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindByUserNameIgnoreCaseAsync(userManager, userName, cancellationToken)
            .ConfigureAwait(false);
        if (existing == null)
            return false;

        return !string.Equals(existing.Id, excludeUserId, StringComparison.OrdinalIgnoreCase);
    }
}
