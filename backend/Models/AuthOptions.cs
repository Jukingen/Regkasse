namespace KasseAPI_Final.Models;

public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// When true, login requests without a clientApp field are accepted in legacy mode
    /// (no app_context claim in token). When false, clientApp is required and requests
    /// without it receive 400.
    /// </summary>
    public bool AllowLegacyLoginWithoutClientApp { get; set; } = false;

    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    public int RefreshTokenLifetimeDays { get; set; } = 14;

    public int ReuseDetectionRevokeLookbackDays { get; set; } = 30;

    /// <summary>
    /// When true, password login is denied if the user has no active <c>user_tenant_memberships</c> row.
    /// Default false: legacy default-tenant snapshot is still used when membership is missing.
    /// </summary>
    public bool RequireTenantMembershipForLogin { get; set; } = false;
}
