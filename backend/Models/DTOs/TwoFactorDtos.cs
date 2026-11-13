using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>POST /api/Auth/verify-2fa — complete SuperAdmin login after TOTP.</summary>
public sealed class VerifyTwoFactorModel
{
    [JsonPropertyName("twoFactorToken")]
    public string? TwoFactorToken { get; set; }

    /// <summary>6-digit authenticator code (or spaces ignored).</summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

/// <summary>
/// Login response when production SuperAdmin 2FA is required.
/// No access/refresh tokens are issued until <see cref="VerifyTwoFactorModel"/> succeeds.
/// </summary>
public sealed class LoginTwoFactorChallengeDto
{
    [JsonPropertyName("requires2FA")]
    public bool Requires2FA { get; set; } = true;

    /// <summary>True when the SuperAdmin has not enrolled an authenticator yet.</summary>
    [JsonPropertyName("requires2FASetup")]
    public bool Requires2FASetup { get; set; }

    [JsonPropertyName("twoFactorToken")]
    public string TwoFactorToken { get; set; } = string.Empty;

    [JsonPropertyName("isDevelopment")]
    public bool IsDevelopment { get; set; }

    /// <summary>Base32 shared secret for authenticator apps (setup only).</summary>
    [JsonPropertyName("authenticatorKey")]
    public string? AuthenticatorKey { get; set; }

    /// <summary>otpauth:// URI for authenticator enrollment (setup only).</summary>
    [JsonPropertyName("authenticatorUri")]
    public string? AuthenticatorUri { get; set; }

    /// <summary>
    /// Present only in Development when 2FA is forced — use as the code in the verify step.
    /// </summary>
    [JsonPropertyName("developmentBypassCode")]
    public string? DevelopmentBypassCode { get; set; }
}
