using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;

namespace KasseAPI_Final.Helpers;

/// <summary>Username change cooldown / new-account rules — SuperAdmin actors are exempt.</summary>
public static class UsernameChangeRestrictions
{
    public static bool IsBypassedForActor(string? actorRole) =>
        string.Equals(
            RoleCanonicalization.GetCanonicalRole(actorRole),
            Roles.SuperAdmin,
            StringComparison.Ordinal);
}
