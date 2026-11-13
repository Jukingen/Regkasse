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

    /// <summary>Access JWT lifetime. Default 24 hours (matches AGENTS.md / JwtSettings.ExpirationHours).</summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 24 * 60;

    public int RefreshTokenLifetimeDays { get; set; } = 14;

    public int ReuseDetectionRevokeLookbackDays { get; set; } = 30;

    /// <summary>
    /// When true, password login is denied if the user has no active <c>user_tenant_memberships</c> row
    /// (SuperAdmin is exempt — platform operator).
    /// Development templates keep this <c>false</c> for DX; Staging/Production templates and ValidateOnStart
    /// require <c>true</c> so misconfigured Production cannot issue JWTs without membership.
    /// </summary>
    public bool RequireTenantMembershipForLogin { get; set; } = false;

    /// <summary>
    /// When true, authenticated requests on mandant subdomain / custom domain hosts must have JWT
    /// <c>tenant_id</c> equal to the Host-resolved tenant. Shared platform hosts (<c>api</c>/<c>pos</c>/<c>admin</c>/<c>www</c>)
    /// and SuperAdmin impersonation tokens are exempt. Emergency kill-switch: set <c>false</c> in Production.
    /// Templates: Development <c>false</c>; base/Staging/Production <c>true</c>.
    /// </summary>
    public bool RequireTenantHostMatch { get; set; } = true;

    /// <summary>
    /// Legacy override for SuperAdmin 2FA. Prefer <c>TwoFactorAuth</c> section.
    /// When null (default): use <c>TwoFactorAuth:Enabled</c> + <c>BypassInDevelopment</c>.
    /// When true/false: force that challenge policy (tests / staging) if <c>TwoFactorAuth:Enabled</c> is true.
    /// </summary>
    public bool? RequireSuperAdminTwoFactor { get; set; }
}
