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

    public sealed class Status
    {
        public int CooldownDays { get; init; }
        public bool CanChange { get; init; }
        public DateTime? LastChangedAtUtc { get; init; }
        public DateTime? NextChangeAllowedAtUtc { get; init; }
    }

    public static async Task<Status> GetStatusAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        bool bypassCooldown = false,
        CancellationToken cancellationToken = default)
    {
        var cooldownDays = (int)MinInterval.TotalDays;
        if (bypassCooldown)
        {
            return new Status
            {
                CooldownDays = cooldownDays,
                CanChange = true,
            };
        }

        var claims = await userManager.GetClaimsAsync(user).ConfigureAwait(false);
        var lastChangeClaim = claims.FirstOrDefault(c => c.Type == LastChangeClaimType);
        if (lastChangeClaim == null)
        {
            return new Status
            {
                CooldownDays = cooldownDays,
                CanChange = true,
            };
        }

        if (!DateTime.TryParse(
                lastChangeClaim.Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var lastChange))
        {
            return new Status
            {
                CooldownDays = cooldownDays,
                CanChange = true,
            };
        }

        lastChange = lastChange.ToUniversalTime();
        var elapsed = DateTime.UtcNow - lastChange;
        if (elapsed >= MinInterval)
        {
            return new Status
            {
                CooldownDays = cooldownDays,
                CanChange = true,
                LastChangedAtUtc = lastChange,
            };
        }

        return new Status
        {
            CooldownDays = cooldownDays,
            CanChange = false,
            LastChangedAtUtc = lastChange,
            NextChangeAllowedAtUtc = lastChange.Add(MinInterval),
        };
    }

    public static async Task<string?> GetRateLimitErrorAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        bool bypassCooldown = false,
        CancellationToken cancellationToken = default)
    {
        if (bypassCooldown)
            return null;

        var status = await GetStatusAsync(userManager, user, bypassCooldown: false, cancellationToken)
            .ConfigureAwait(false);
        if (status.CanChange || status.NextChangeAllowedAtUtc is not DateTime retryAt)
            return null;

        return FormatBlockedMessage(retryAt);
    }

    private static string FormatBlockedMessage(DateTime retryAt) =>
        $"Username can only be changed once every {MinInterval.Days} days. Next change allowed after {retryAt:yyyy-MM-dd HH:mm} UTC.";

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
