namespace KasseAPI_Final.Authorization;

/// <summary>
/// Per-client-app role policies for login gate.
/// Each client application (POS terminal, Admin panel) defines which canonical roles
/// are allowed to authenticate. Fail-closed: unknown clientApp values are rejected.
/// Role names reference <see cref="Roles"/> constants (canonical, post-migration).
/// </summary>
public static class ClientAppPolicy
{
    public const string Pos = "pos";
    public const string Admin = "admin";

    /// <summary>JWT custom claim key persisted in the token after login.</summary>
    public const string AppContextClaimType = "app_context";

    /// <summary>Optional HTTP header for anonymous clients (e.g. POS kiosk) when no JWT app_context claim exists.</summary>
    public const string AppContextHttpHeader = "X-App-Context";

    private static readonly IReadOnlySet<string> PosAllowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Roles.Cashier,
        Roles.SuperAdmin,
    };

    private static readonly IReadOnlySet<string> AdminAllowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Roles.SuperAdmin,
        Roles.Manager,
        Roles.Accountant,
        Roles.ReportViewer,
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Policies =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [Pos] = PosAllowedRoles,
            [Admin] = AdminAllowedRoles,
        };

    /// <summary>All recognised clientApp values.</summary>
    public static readonly IReadOnlyList<string> KnownApps = new[] { Pos, Admin };

    /// <returns>True when <paramref name="clientApp"/> is a known value ("pos", "admin").</returns>
    public static bool IsKnownApp(string? clientApp)
        => !string.IsNullOrWhiteSpace(clientApp) && Policies.ContainsKey(clientApp);

    /// <summary>
    /// Checks whether any of the user's roles is allowed for the given client app.
    /// Returns false when clientApp is unknown or roles is empty.
    /// </summary>
    public static bool IsRoleAllowedForApp(string clientApp, IEnumerable<string> roles)
    {
        if (!Policies.TryGetValue(clientApp, out var allowed))
            return false;

        foreach (var role in roles)
        {
            var canonical = Auth.RoleCanonicalization.GetCanonicalRole(role);
            if (allowed.Contains(canonical))
                return true;
        }

        return false;
    }

    /// <summary>Single-role convenience overload.</summary>
    public static bool IsRoleAllowedForApp(string clientApp, string role)
        => IsRoleAllowedForApp(clientApp, new[] { role });

    /// <summary>
    /// Returns whether the role can log into POS. Uses canonical role for lookup.
    /// Used by role capability matrix API only; does not change login authorization.
    /// </summary>
    public static bool CanLoginToPos(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return false;
        var canonical = Auth.RoleCanonicalization.GetCanonicalRole(roleName);
        return PosAllowedRoles.Contains(canonical);
    }

    /// <summary>
    /// Returns whether the role can log into Admin. Uses canonical role for lookup.
    /// Used by role capability matrix API only; does not change login authorization.
    /// </summary>
    public static bool CanLoginToAdmin(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return false;
        var canonical = Auth.RoleCanonicalization.GetCanonicalRole(roleName);
        return AdminAllowedRoles.Contains(canonical);
    }
}
