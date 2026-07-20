namespace KasseAPI_Final.Configuration;

/// <summary>
/// SuperAdmin TOTP 2FA policy. Bound from <c>TwoFactorAuth</c> configuration section.
/// </summary>
public sealed class TwoFactorAuthOptions
{
    public const string SectionName = "TwoFactorAuth";

    /// <summary>
    /// Master switch. When <c>false</c>, SuperAdmin login never requires a 2FA challenge.
    /// Production should keep this <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When <c>true</c> and the host environment is Development, skip the 2FA challenge at login
    /// (password success issues tokens immediately). Ignored outside Development (fail-closed).
    /// </summary>
    public bool BypassInDevelopment { get; set; } = true;

    /// <summary>
    /// Accepted as a TOTP substitute only in Development (together with <c>DEV-2FA-BYPASS</c>).
    /// Never honored in Production / Staging.
    /// </summary>
    public string TestToken { get; set; } = "123456";
}
