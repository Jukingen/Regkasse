namespace KasseAPI_Final.Configuration;

/// <summary>
/// Double-submit CSRF protection for state-changing HTTP methods.
/// Bound from <c>Security:Csrf</c>. Disabled by default; enable in Production.
/// </summary>
public sealed class CsrfOptions
{
    public const string SectionName = "Security:Csrf";

    /// <summary>When false, middleware is a no-op.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// When true and the host is Development, skip CSRF validation.
    /// Ignored outside Development (fail-closed, same pattern as TwoFactorAuth).
    /// </summary>
    public bool BypassInDevelopment { get; set; } = true;

    /// <summary>Request header that must carry the CSRF token (Angular-style X-XSRF-TOKEN).</summary>
    public string HeaderName { get; set; } = "X-XSRF-TOKEN";

    /// <summary>Cookie name that must match the header token.</summary>
    public string CookieName { get; set; } = "XSRF-TOKEN";

    /// <summary>Token lifetime in hours (cache + cookie Max-Age).</summary>
    public int TokenLifetimeHours { get; set; } = 24;
}
