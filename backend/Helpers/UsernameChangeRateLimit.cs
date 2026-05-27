using System.Globalization;
using System.Security.Claims;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;

namespace KasseAPI_Final.Helpers;

/// <summary>Per-user cooldown between login username changes (stored as Identity claim).</summary>
public static class UsernameChangeRateLimit
{
    public const string LastChangeClaimType = "LastUsernameChange";

    public static readonly TimeSpan MinInterval = TimeSpan.FromDays(7);

    public static async Task<string?> GetRateLimitErrorAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var claims = await userManager.GetClaimsAsync(user).ConfigureAwait(false);
        var lastChangeClaim = claims.FirstOrDefault(c => c.Type == LastChangeClaimType);
        if (lastChangeClaim == null)
            return null;

        if (!DateTime.TryParse(
                lastChangeClaim.Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var lastChange))
            return null;

        lastChange = lastChange.ToUniversalTime();
        var elapsed = DateTime.UtcNow - lastChange;
        if (elapsed >= MinInterval)
            return null;

        var retryAt = lastChange.Add(MinInterval);
        return $"Username can only be changed once every {MinInterval.Days} days. Next change allowed after {retryAt:yyyy-MM-dd HH:mm} UTC.";
    }

    public static async Task RecordChangeAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var claims = await userManager.GetClaimsAsync(user).ConfigureAwait(false);
        var existing = claims.FirstOrDefault(c => c.Type == LastChangeClaimType);
        var value = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var updated = new Claim(LastChangeClaimType, value);

        if (existing != null)
            await userManager.ReplaceClaimAsync(user, existing, updated).ConfigureAwait(false);
        else
            await userManager.AddClaimAsync(user, updated).ConfigureAwait(false);
    }
}
