using KasseAPI_Final.Models;

namespace KasseAPI_Final.Helpers;

/// <summary>Business rules for admin-initiated login username changes.</summary>
public static class UsernameChangePolicy
{
    public static readonly TimeSpan MinAccountAgeBeforeChange = TimeSpan.FromHours(24);

    public static string? GetNewAccountRestrictionError(ApplicationUser user, bool bypassRestrictions = false)
    {
        if (bypassRestrictions)
            return null;

        var age = DateTime.UtcNow - user.CreatedAt.ToUniversalTime();
        if (age < MinAccountAgeBeforeChange)
        {
            return $"Username cannot be changed within {MinAccountAgeBeforeChange.TotalHours:0} hours of account creation.";
        }

        return null;
    }
}
