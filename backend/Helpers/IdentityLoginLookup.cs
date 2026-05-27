using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Helpers;

/// <summary>
/// Case-insensitive login and username uniqueness using <see cref="ApplicationUser.NormalizedUserName"/>.
/// </summary>
public static class IdentityLoginLookup
{
    public static string NormalizeUserName(UserManager<ApplicationUser> userManager, string userName) =>
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
        var user = userManager.Users
            .FirstOrDefault(u => u.NormalizedUserName == normalized);

        if (user != null)
            return await Task.FromResult<ApplicationUser?>(user).ConfigureAwait(false);

        // Legacy rows without NormalizedUserName backfill
        var upper = trimmed.ToUpperInvariant();
        user = userManager.Users
            .FirstOrDefault(u =>
                u.NormalizedUserName == null
                && u.UserName != null
                && u.UserName.ToUpper() == upper);

        return await Task.FromResult(user).ConfigureAwait(false);
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
