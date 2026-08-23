namespace KasseAPI_Final.Configuration;

/// <summary>
/// HttpOnly auth cookies for browser clients. POS/native still uses JSON Bearer tokens
/// as the source of truth; POS web may also receive <c>rk_pos_*</c> cookies.
/// Bound from <c>AuthCookies</c>.
/// FA Edge <c>proxy.ts</c> reads <see cref="AdminAccessCookieName"/> when
/// <see cref="Domain"/> is a parent domain (e.g. <c>.regkasse.at</c>).
/// </summary>
public sealed class AuthCookieOptions
{
    public const string SectionName = "AuthCookies";

    public const string AdminAccessCookieName = "rk_admin_access_token";
    public const string AdminRefreshCookieName = "rk_admin_refresh_token";
    public const string PosAccessCookieName = "rk_pos_access_token";
    public const string PosRefreshCookieName = "rk_pos_refresh_token";

    /// <summary>Pre-split shared cookie; still read and expired so leftover sessions do not collide.</summary>
    public const string LegacyAccessCookieName = "access_token";

    /// <summary>Pre-split shared cookie; still read and expired so leftover sessions do not collide.</summary>
    public const string LegacyRefreshCookieName = "refresh_token";

    /// <summary>When false, login/refresh/logout do not write or clear auth cookies.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Extra access-cookie alias to read/clear (default: legacy <c>access_token</c>).
    /// Writes always use <see cref="AdminAccessCookieName"/> / <see cref="PosAccessCookieName"/>.
    /// </summary>
    public string AccessCookieName { get; set; } = LegacyAccessCookieName;

    /// <summary>
    /// Extra refresh-cookie alias to read/clear (default: legacy <c>refresh_token</c>).
    /// Writes always use <see cref="AdminRefreshCookieName"/> / <see cref="PosRefreshCookieName"/>.
    /// </summary>
    public string RefreshCookieName { get; set; } = LegacyRefreshCookieName;

    /// <summary>
    /// Optional parent domain (e.g. <c>.regkasse.at</c>) so FA Edge <c>proxy.ts</c> can read the
    /// HttpOnly admin access cookie. Leave empty for host-only cookies (API origin only).
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// When null, Secure is off in Development and on otherwise.
    /// SameSite=None always forces Secure=true (browser requirement).
    /// </summary>
    public bool? Secure { get; set; }

    /// <summary>
    /// <c>Lax</c> (production same-site admin↔api), <c>Strict</c>, or <c>None</c>
    /// (cross-site XHR). Empty: Development → None, otherwise Lax.
    /// </summary>
    public string SameSite { get; set; } = "";
}
