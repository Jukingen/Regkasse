namespace KasseAPI_Final.Helpers;

/// <summary>Reserved login names that cannot be assigned to users.</summary>
public static class ReservedUsernames
{
    private static readonly HashSet<string> Blocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "root",
        "system",
        "support",
        "helpdesk",
        "superadmin",
        "superuser",
        "administrator",
        "moderator",
        "owner",
    };

    public static bool IsReserved(string? userName) =>
        !string.IsNullOrWhiteSpace(userName) && Blocked.Contains(userName.Trim());
}
